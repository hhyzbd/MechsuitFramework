﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Exosuit
{

    public class StatWorker_Module : StatWorker
    {
        public override bool ShouldShowFor(StatRequest req)
        {
            return base.ShouldShowFor(req) && req.HasThing && req.Thing.HasComp<CompSuitModule>();
        }
        public override IEnumerable<Dialog_InfoCard.Hyperlink> GetInfoCardHyperlinks(StatRequest statRequest)
        {
            CompSuitModule ext = statRequest.Thing.TryGetComp<CompSuitModule>();
            if (ext != null)
            {
                foreach (SlotDef slot in ext.Props.occupiedSlots)
                {
                    yield return new Dialog_InfoCard.Hyperlink(slot);
                }
            }
        }
        public override string GetExplanationFinalizePart(StatRequest req, ToStringNumberSense numberSense, float finalVal)
        {
            CompProperties_ExosuitModule comp = req.Thing.TryGetComp<CompSuitModule>().Props;
            string s = "WG_Stats_TakingSlotsOf".Translate() + "\n";
            foreach (var item in comp.occupiedSlots)
            {
                s += "  " + item.LabelCap + "\n";
            }
            if (!comp.disabledSlots.NullOrEmpty())
            {
                s += "WG_Stats_DisableSlotsOf".Translate() + "\n";
                foreach (var item in req.Thing.TryGetComp<CompSuitModule>().Props.disabledSlots)
                {
                    s += "  " + item.LabelCap + "\n";
                }
            }
            return s;
        }
        public override string GetStatDrawEntryLabel(StatDef stat, float value, ToStringNumberSense numberSense, StatRequest optionalReq, bool finalized = true)
        {
            var slots = optionalReq.Thing.TryGetComp<CompSuitModule>().Props.occupiedSlots;
            string str = "";
            for (int i = 0; i < slots.Count; i++)
            {
                SlotDef item = slots[i];
                str += item.LabelCap;
                if(i != slots.Count-1) str += ", ";
            }
            return str;
        }
    }
}

