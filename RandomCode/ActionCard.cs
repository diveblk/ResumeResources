using System;
using System.Collections.Generic;
using System.Linq;
using DrawnIntoDarkness.Cards.AttackModifierCards;
using DrawnIntoDarkness.Decks;
using DrawnIntoDarkness.Entities;
using DrawnIntoDarkness.Godot.Scenes.Entities.Player;
using DrawnIntoDarkness.Managers;
using static DrawnIntoDarkness.Cards.AttackModifierCards.DamageMetadata;
using static DrawnIntoDarkness.Helpers.Enums.GeneralEnums;

namespace DrawnIntoDarkness.Cards.ActionCards
{
    // Prefer public so reflection/other assemblies can construct these safely.
    public abstract class ActionCardBase : IActionCard
    {
        // --- ICard ---
        public Guid Id { get; } = Guid.NewGuid();
        public ICard.CardType Type => ICard.CardType.Action;

        // --- Required by concrete cards ---
        public abstract string Name { get; }
        public abstract string Description { get; }

        // --- Defaults you can override per card ---
        public virtual ActionSpeed Speed => ActionSpeed.Normal;
        public virtual int CooldownRounds => 0;
        public virtual ExhaustScope Exhausts => ExhaustScope.Never;
        public virtual TargetKind Targeting => TargetKind.Self;
        public virtual int DurationTurns => 0;     // 0 = instant
        public virtual bool RequiresConcentration => false;

        // --- Lifecycle (you’ll typically override Begin only) ---
        public virtual OngoingActionHandle? Begin(ActionContext ctx, IReadOnlyList<object> selectedTargets)
            => null; // instant no-op by default

        public virtual void Tick(ActionContext ctx, OngoingActionHandle h) { }   // default no-op
        public virtual void Cancel(ActionContext ctx, OngoingActionHandle h) { } // default no-op

        public virtual bool CanBegin(ActionContext ctx, out string? reason)
        { reason = null; return true; }

        // ---------- Helpers for common patterns ----------

        /// Pick a target entity: prefer an explicitly selected IEntity, else first enemy, else null.
        protected IEntity? SelectTarget(ActionContext ctx, IReadOnlyList<object> selectedTargets)
            => selectedTargets.OfType<IEntity>().FirstOrDefault() ?? ctx.Enemies.FirstOrDefault();

        /// Draw attack(s) with rolling/advantage, apply to target, discard drawn cards.
        /// Returns the final damage applied.
        protected int DealAttackCombo(ActionContext ctx, IEntity target)
        {
            var atkAdv = ctx.Advantage.Consume(ctx.Caster, DeckTarget.Attack);
            var roll = ctx.Decks.DrawAttackChain(atkAdv);

            // Let caster statuses tweak outgoing damage (e.g., Weakened)
            int baseDmg = roll.Total;
            var outInfo = new DamageInfo { Attacker = ctx.Caster, Kind = DamageKind.Attack, Source = this };

            if (ctx.Caster is EntityBase src && baseDmg > 0)
            {
                baseDmg = ApplyOutgoingDamageMods(src, baseDmg, in outInfo);
            }

            var defAdv = ctx.Advantage.Consume(target, DeckTarget.Defense);

            int before = (target as EntityBase)?.HP ?? 0;
            target.TakeDamage(baseDmg, true, defAdv, ctx.Caster, DamageKind.Attack, this);
            int after = (target as EntityBase)?.HP ?? 0;
            int dealt = Math.Max(0, before - after);

            foreach (var c in roll.Drawn)
                ctx.Decks.Discard(DeckTarget.Attack, c);

            // notify lifesteal/etc.
            if (ctx.Caster is EntityBase src2 && dealt > 0)
                src2.NotifyDealtDamage(dealt, in outInfo);

            return dealt;
        }

        /// Build (but don’t schedule) an ongoing handle; the runtime should call StartIfOngoing().
        protected OngoingActionHandle MakeHandle(ActionContext ctx, IReadOnlyList<object> targets,
                                                 int? durationOverride = null, object? userData = null)
        {
            return new OngoingActionHandle
            {
                Card = this,
                Caster = ctx.Caster,
                Targets = targets,
                RemainingTurns = durationOverride ?? Math.Max(1, DurationTurns),
                Concentration = RequiresConcentration,
                UserData = userData
            };
        }

        static int ApplyOutgoingDamageMods(EntityBase src, int current, in DamageInfo info)
        {
            int value = current;

            // 1) Equipment-based mods (if the entity has equipment)
            if (src is IHasEquipment he && he.Equipment != null)
            {
                foreach (var eq in he.Equipment.EnumerateEquipped())
                {
                    if (eq == null || eq.IsVeiled) continue;
                    if (eq is IOutgoingDamageModifier mod)
                        value = Math.Max(0, mod.ModifyOutgoingDamage(src, value, in info));
                }
            }

            // 2) Status-based mods (existing behavior)
            foreach (var s in src.Statuses)
                if (s is IOutgoingDamageModifier mod)
                    value = Math.Max(0, mod.ModifyOutgoingDamage(src, value, in info));

            return value;
        }
    }
}
