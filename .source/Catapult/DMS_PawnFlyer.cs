﻿using Exosuit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace Exosuit
{
    [StaticConstructorOnStartup]
    public class WG_PawnFlyer : PawnFlyer
    {
        #region 太空弹射...
        #endregion
        //发射源建筑		修改人：Anxie,日期：2024-09-09
        public Thing eBay;

        //是否为着陆		修改人：Anxie,日期：2024-09-09
        public bool isLanding;

        //是否为投射到大地图		修改人：Anxie,日期：2024-09-09
        public bool isToMap;

        public Map destMap;

        public LocalTargetInfo actionTarget;

        public Pawn pawnStored;

        public static readonly Texture2D TargeterMouseAttachment = ContentFinder<Texture2D>.Get("UI/Overlays/LaunchableMouseAttachment");

        

        //射程的接口		修改人：Anxie,日期：2024-09-09
        public ModExtension_Flyer Extension => def.GetModExtension<ModExtension_Flyer>();

        /*public override Vector3 DrawPos
        {
            get
            {
                RecomputePosition();
                return effectivePos;
            }
        }*/

        new public Vector3 DestinationPos
        {
            get
            {
                if (FlyingThing != null)
                {
                    Thing flyingThing = FlyingPawn;
                    return GenThing.TrueCenter(destCell, flyingThing.Rotation, flyingThing.def.size, flyingThing.def.Altitude);
                }
                else
                    return destCell.ToVector3();
            }
        }

        /*protected new virtual void RecomputePosition()
        {
            if (positionLastComputedTick != ticksFlying)
            {
                positionLastComputedTick = ticksFlying;
                float t = (float)ticksFlying / (float)ticksFlightTime;
                float t2 = def.pawnFlyer.Worker.AdjustedProgress(t);
                effectiveHeight = def.pawnFlyer.Worker.GetHeight(t2);
                groundPos = Vector3.Lerp(startVec, DestinationPos, t2);
                Vector3 vector = Altitudes.AltIncVect * effectiveHeight;
                Vector3 vector2 = Vector3.forward * (def.pawnFlyer.heightFactor * effectiveHeight);
                effectivePos = groundPos + vector + vector2;
                base.Position = groundPos.ToIntVec3();
            }
        }*/

        /*public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }*/


        public override void RespawnPawn()
        {
            Thing flyingThing = FlyingThing;
            LandingEffects();

            innerContainer.TryDrop(flyingThing, destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out var lastResultingThing, null, null, playDropSound: false);
            Pawn pawn = flyingThing as Pawn;
            if (pawn?.drafter != null)
            {
                pawn.drafter.Drafted = pawnWasDrafted;
                pawn.drafter.FireAtWill = pawnCanFireAtWill;
            }
            flyingThing.Rotation = Rotation;

            if (carriedThing != null && innerContainer.TryDrop(carriedThing, destCell, flyingThing.MapHeld, ThingPlaceMode.Direct, out lastResultingThing, null, null, playDropSound: false) && pawn != null)
            {
                carriedThing.DeSpawn();
                if (!pawn.carryTracker.TryStartCarry(carriedThing))
                {
                    Log.Error("Could not carry " + carriedThing.ToStringSafe() + " after respawning flyer pawn.");
                }
            }

            if (pawn == null)
            {
                return;
            }

            if(ToDiffMapTarget(pawn))return;

            if (jobQueue != null)
            {
                pawn.jobs.RestoreCapturedJobs(jobQueue);
            }

            pawn.jobs.CheckForJobOverride();
            if (def.pawnFlyer.stunDurationTicksRange != IntRange.Zero)
            {
                pawn.stances.stunner.StunFor(def.pawnFlyer.stunDurationTicksRange.RandomInRange, null, addBattleLog: false, showMote: false);
            }

            if (triggeringAbility == null)
            {
                return;
            }

            Ability ability = pawn.abilities.GetAbility(triggeringAbility);
            if (ability?.comps == null)
            {
                return;
            }
            foreach (AbilityComp comp in ability.comps)
            {
                if (comp is ICompAbilityEffectOnJumpCompleted compAbilityEffectOnJumpCompleted)
                {
                    compAbilityEffectOnJumpCompleted.OnJumpCompleted(startVec.ToIntVec3(), target);
                }
            }
        }

        protected virtual bool ToDiffMapTarget(Pawn pawn)
        {
            if (isToMap)
            {
                //投射到大地图		修改人：Anxie,日期：2024-09-09
                pawn.DeSpawn();
                int extraRange = (int)pawn.GetStatValue(StatDefof.MF_FlightRange);
                GenSpawn.Spawn(pawn, new IntVec3(1, 0, 1), destMap);
                Caravan caravan = CaravanMaker.MakeCaravan(new List<Pawn> { pawn }, pawn.Faction, pawn.Map.Tile, addToWorldPawnsIfNotAlready: false);
                pawn.ExitMap(allowedToJoinOrCreateCaravan: false, Rot4.North);
                CameraJumper.TryJump(CameraJumper.GetWorldTarget(this));
                Find.WorldSelector.ClearSelection();
                int tile = this.Map.Tile;
                pawnStored = pawn;
                Find.WorldTargeter.BeginTargeting(ChoseWorldTarget, canTargetTiles: true, TargeterMouseAttachment, closeWorldTabWhenFinished: false, delegate
                {
                    GenDraw.DrawWorldRadiusRing(tile, Extension.flightRange + extraRange);
                }, (GlobalTargetInfo target) => TargetingLabelGetter(target, tile, Extension.flightRange + extraRange, new List<IThingHolder> { (Building_EjectorBay)eBay }, TryLaunch, null));
                return true;
            }
            if (!isLanding)
            {
                Log.Message(GetDirectlyHeldThings().ContentsString);
                pawn.DeSpawn();
                GenSpawn.Spawn(pawn, new IntVec3(actionTarget.Cell.x, actionTarget.Cell.y, actionTarget.Cell.z + 25 < destMap.AllCells.MaxBy(o => o.z).z ? actionTarget.Cell.z + 25 : destMap.AllCells.MaxBy(o => o.z).z), destMap);
                pawn.Rotation = Rot4.South;
                WG_AbilityVerb_QuickJump.DoJump(pawn, destMap, target, actionTarget.Cell, true, false);
                return true;
            }
            GenExplosion.DoExplosion(actionTarget.Cell, destMap, 8, DefDatabase<DamageDef>.GetNamed("Bomb"), null, damAmount: 50, ignoredThings: [pawn], excludeRadius: 1);
            return false;
        }

        /// <summary>
        /// 发射到大地图
        /// </summary>
        /// <param name="destinationTile"></param>
        /// <param name="arrivalAction"></param>
        public void TryLaunch(int destinationTile, TransportersArrivalAction arrivalAction)
        {
            if (pawnStored.GetCaravan() != null)
            {
                pawnStored.GetCaravan().Tile = destinationTile;
            }
        }

        /// <summary>
        /// 发射到设施或基地
        /// </summary>
        /// <param name="destinationTile"></param>
        /// <param name="arrivalAction"></param>
        public void TryLaunchToSiteOrSettleMent(int destinationTile, TransportersArrivalAction arrivalAction)
        {
            if (Find.World.worldObjects.AnySiteAt(destinationTile))
            {
                Site site = (Site)Find.World.worldObjects.AllWorldObjects.FirstOrDefault(o => o.Tile == destinationTile);
                if (pawnStored.GetCaravan() != null)
                {
                    bool num = !site.HasMap;
                    Map orGenerateMap = null;
                    LongEventHandler.SetCurrentEventText("GenerateSubMap".Translate());
                    DeepProfiler.Start("Generate map");
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, site.def);
                        TaggedString letterLabel = "LetterLabelCaravanEnteredEnemyBase".Translate();
                        TaggedString letterText = "LetterTransportPodsLandedInEnemyBase".Translate(site.Label).CapitalizeFirst();
                        SettlementUtility.AffectRelationsOnAttacked(site, ref letterText);
                        if (num)
                        {
                            GenStep_Fog.UnfogMapFromEdge(orGenerateMap);
                            Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                            PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter(orGenerateMap.mapPawns.AllPawns, ref letterLabel, ref letterText, "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural), informEvenIfSeenBefore: true);
                            CaravanEnterMapUtility.Enter(pawnStored.GetCaravan(), orGenerateMap, CaravanEnterMode.Center, CaravanDropInventoryMode.DoNotDrop);
                            CameraJumper.TryJump(pawnStored);
                            pawnStored.DeSpawn();
                            Messages.Message(new Message("请选择着陆地点", MessageTypeDefOf.PositiveEvent));

                            Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), null, delegate (LocalTargetInfo target)
                            {
                                SkyfallerMaker.SpawnSkyfaller(RimWorld.ThingDefOf.MeteoriteIncoming, pawnStored, target.Cell, Find.CurrentMap);
                                //GenExplosion.DoExplosion(pawnStored.Position, orGenerateMap, 10, DefDatabase<DamageDef>.GetNamed("Bomb"), null, 50);
                                pawnStored.SetPositionDirect(target.Cell);
                                CameraJumper.TryJump(pawnStored);
                            }, null, TargeterMouseAttachment);
                        }
                    }, "GeneratingMap".Translate(), true, (Exception x) => { Log.Message("Generatem Map Error:" + x.ToString()); });
                }
            }
            else if (Find.World.worldObjects.AnySettlementAt(destinationTile))
            {
                Settlement site = (Settlement)Find.World.worldObjects.AllWorldObjects.FirstOrDefault(o => o.Tile == destinationTile);
                if (pawnStored.GetCaravan() != null)
                {
                    bool num = !site.HasMap;
                    Map orGenerateMap = null;
                    LongEventHandler.SetCurrentEventText("GenerateSubMap".Translate());
                    DeepProfiler.Start("Generate map");
                    LongEventHandler.QueueLongEvent(() =>
                    {
                        orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile, site.def);
                        TaggedString letterLabel = "LetterLabelCaravanEnteredEnemyBase".Translate();
                        TaggedString letterText = "LetterTransportPodsLandedInEnemyBase".Translate(site.Label).CapitalizeFirst();
                        SettlementUtility.AffectRelationsOnAttacked(site, ref letterText);
                        if (num)
                        {
                            GenStep_Fog.UnfogMapFromEdge(orGenerateMap);
                            Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                            PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter(orGenerateMap.mapPawns.AllPawns, ref letterLabel, ref letterText, "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural), informEvenIfSeenBefore: true);
                            CaravanEnterMapUtility.Enter(pawnStored.GetCaravan(), orGenerateMap, CaravanEnterMode.Center, CaravanDropInventoryMode.DoNotDrop);
                            CameraJumper.TryJump(pawnStored);
                            pawnStored.DeSpawn();
                            Messages.Message(new Message("请选择着陆地点", MessageTypeDefOf.PositiveEvent));

                            Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), null, delegate (LocalTargetInfo target)
                            {
                                SkyfallerMaker.SpawnSkyfaller(RimWorld.ThingDefOf.MeteoriteIncoming, pawnStored, target.Cell, Find.CurrentMap);
                                //GenExplosion.DoExplosion(pawnStored.Position, orGenerateMap, 10, DefDatabase<DamageDef>.GetNamed("Bomb"), null, 50);
                                pawnStored.SetPositionDirect(target.Cell);
                                CameraJumper.TryJump(pawnStored);
                            }, null, TargeterMouseAttachment);
                        }
                    }, "GeneratingMap".Translate(), true, (Exception x) => { Log.Message("Generatem Map Error:" + x.ToString()); });



                }
            }
        }
        private void LandingInMap(TargetInfo target)
        {
            soundLanding?.PlayOneShot(new TargetInfo(base.Position, base.Map));
            FleckMaker.ThrowDustPuff(DestinationPos + Gen.RandomHorizontalVector(0.5f), base.Map, 2f);
        }
        /// <summary>
        /// 选择目标
        /// </summary>
        public bool ChoseWorldTarget(GlobalTargetInfo target, int tile, IEnumerable<IThingHolder> pods, int maxLaunchDistance, Action<int, TransportersArrivalAction> launchAction, CompLaunchable launchable)
        {
            if (!target.IsValid)
            {
                Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            int num = Find.WorldGrid.TraversalDistanceBetween(tile, target.Tile);
            if (maxLaunchDistance > 0 && num > maxLaunchDistance)
            {
                Messages.Message("TransportPodDestinationBeyondMaximumRange".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            IEnumerable<FloatMenuOption> source = GetOptionsForTile(target.Tile, pods, launchAction);
            if (!source.Any())
            {
                if (Find.World.Impassable(target.Tile))
                {
                    Messages.Message("MessageTransportPodsDestinationIsInvalid".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }

                launchAction(target.Tile, null);
                return true;
            }

            if (source.Count() == 1)
            {
                if (!source.First().Disabled)
                {
                    source.First().action();
                    return true;
                }

                return false;
            }

            Find.WindowStack.Add(new FloatMenu(source.ToList()));
            return true;
        }
        /// <summary>
        /// 着陆的浮动菜单
        /// </summary>
        /// <param name="target"></param>
        /// <param name="tile"></param>
        /// <param name="maxLaunchDistance"></param>
        /// <param name="pods"></param>
        /// <param name="launchAction"></param>
        /// <param name="launchable"></param>
        /// <returns></returns>
        public string TargetingLabelGetter(GlobalTargetInfo target, int tile, int maxLaunchDistance, IEnumerable<IThingHolder> pods, Action<int, TransportersArrivalAction> launchAction, CompLaunchable launchable)
        {
            if (!target.IsValid)
            {
                return null;
            }

            int num = Find.WorldGrid.TraversalDistanceBetween(tile, target.Tile);
            if (maxLaunchDistance > 0 && num > maxLaunchDistance)
            {
                GUI.color = ColorLibrary.RedReadable;
                return "TransportPodDestinationBeyondMaximumRange".Translate();
            }

            IEnumerable<FloatMenuOption> source = GetOptionsForTile(target.Tile, pods, launchAction);
            if (!source.Any())
            {
                return string.Empty;
            }

            if (source.Any())
            {
                if (source.First().Disabled)
                {
                    GUI.color = ColorLibrary.RedReadable;
                }

                return source.First().Label;
            }

            if (target.WorldObject is MapParent mapParent)
            {
                return "ClickToSeeAvailableOrders_WorldObject".Translate(mapParent.LabelCap);
            }

            return "ClickToSeeAvailableOrders_Empty".Translate();
        }
        public bool ChoseWorldTarget(GlobalTargetInfo target)
        {
            return ChoseWorldTarget(target, eBay.Map.Tile, new List<IThingHolder> { (Building_EjectorBay)eBay }, Extension.flightRange + (int)pawnStored.GetStatValue(StatDefof.MF_FlightRange), TryLaunch, null);
        }
        /// <summary>
        /// 按目标分配情况
        /// </summary>
        /// <param name="tile"></param>
        /// <param name="pods"></param>
        /// <param name="launchAction"></param>
        /// <returns></returns>
        public IEnumerable<FloatMenuOption> GetOptionsForTile(int tile, IEnumerable<IThingHolder> pods, Action<int, TransportersArrivalAction> launchAction)
        {
            if (!Find.World.Impassable(tile) && pawnStored.GetCaravan() != null)
            {
                yield return new FloatMenuOption("WG_EjectToTile".Translate(), delegate
                {
                    TryLaunch(tile, null);
                });
            }
            if (Find.World.worldObjects.AnySettlementBaseAtOrAdjacent(tile) || Find.World.worldObjects.AnySiteAt(tile))
            {
                yield return new FloatMenuOption("WG_EjectToBase".Translate(), delegate
                {
                    TryLaunchToSiteOrSettleMent(tile, null);
                });
            }
        }


        public static WG_PawnFlyer MakeFlyer(ThingDef flyingDef, Pawn pawn, IntVec3 destCell, LocalTargetInfo actionTarget, Map destMap, EffecterDef flightEffecterDef, SoundDef landingSound, bool isLanding, bool isToMap, bool flyWithCarriedThing = false, Vector3? overrideStartVec = null, Ability triggeringAbility = null, LocalTargetInfo target = default)
        {
            WG_PawnFlyer pawnFlyer = (WG_PawnFlyer)ThingMaker.MakeThing(flyingDef);
            pawnFlyer.startVec = overrideStartVec ?? pawn.TrueCenter();
            pawnFlyer.Rotation = Rot4.North;
            pawnFlyer.flightDistance = pawn.Position.DistanceTo(destCell);
            pawnFlyer.destCell = destCell;
            pawnFlyer.destMap = destMap;
            pawnFlyer.isToMap = isToMap;
            pawnFlyer.actionTarget = actionTarget;
            pawnFlyer.isLanding = isLanding;
            pawnFlyer.pawnWasDrafted = pawn.Drafted;
            pawnFlyer.flightEffecterDef = flightEffecterDef;
            pawnFlyer.soundLanding = landingSound;
            pawnFlyer.triggeringAbility = triggeringAbility?.def;
            pawnFlyer.target = target;
            if (pawn.drafter != null)
            {
                pawnFlyer.pawnCanFireAtWill = pawn.drafter.FireAtWill;
            }

            if (pawn.CurJob != null)
            {
                if (pawn.CurJob.def == RimWorld.JobDefOf.CastJump)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
                else
                {
                    pawn.jobs.SuspendCurrentJob(JobCondition.InterruptForced);
                }
            }

            pawnFlyer.jobQueue = pawn.jobs.CaptureAndClearJobQueue();
            if (flyWithCarriedThing && pawn.carryTracker.CarriedThing != null && pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out pawnFlyer.carriedThing))
            {
                pawnFlyer.carriedThing.holdingOwner?.Remove(pawnFlyer.carriedThing);

                pawnFlyer.carriedThing.DeSpawn();
            }

            if (pawn.Spawned)
            {
                pawn.DeSpawn(DestroyMode.WillReplace);
            }

            if (!pawnFlyer.innerContainer.TryAdd(pawn))
            {
                Log.Error("Could not add " + pawn.ToStringSafe() + " to a flyer.");
                pawn.Destroy();
            }

            if (pawnFlyer.carriedThing != null && !pawnFlyer.innerContainer.TryAdd(pawnFlyer.carriedThing))
            {
                Log.Error("Could not add " + pawnFlyer.carriedThing.ToStringSafe() + " to a flyer.");
            }

            return pawnFlyer;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref eBay, "eBay");
            Scribe_Values.Look(ref isLanding, "isLanding");
            Scribe_Values.Look(ref isToMap, "isToMap");
            Scribe_References.Look(ref destMap, "destMap");
            Scribe_TargetInfo.Look(ref actionTarget, "target");
            Scribe_Deep.Look(ref pawnStored, "pawnStored");
        }
    }
    public class ModExtension_Flyer : DefModExtension
    {
        public int flightRange;
    }
}