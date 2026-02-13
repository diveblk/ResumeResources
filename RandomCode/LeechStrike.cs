using DrawnIntoDarkness.Cards.ActionCards.StatusEffect;
using DrawnIntoDarkness.Entities;
using DrawnIntoDarkness.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DrawnIntoDarkness.Helpers.Enums.GeneralEnums;

namespace DrawnIntoDarkness.Cards.ActionCards
{
    public sealed class LeechStrike : ActionCardBase
    {
        public override string Name => "Leech Strike";
        public override string Description => "Gain lifesteal (50%) for 2 turns, then strike.";
        public override TargetKind Targeting => TargetKind.SingleEnemy;

        public override OngoingActionHandle? Begin(ActionContext ctx, IReadOnlyList<object> selected)
        {
            if (ctx.Caster is EntityBase self)
                self.ApplyStatus(new LifestealStatus(ratio: 0.5f, turns: 2));

            var t = SelectTarget(ctx, selected); if (t is null) return null;
            DealAttackCombo(ctx, t);
            return null;
        }
    }

}
