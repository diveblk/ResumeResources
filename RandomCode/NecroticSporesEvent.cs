using DrawnIntoDarkness.Cards.ActionCards.StatusEffect;
using DrawnIntoDarkness.Entities;
using System.Collections.Generic;
using static DrawnIntoDarkness.Cards.EventCards.EventPayloads;
using static DrawnIntoDarkness.Helpers.Enums.GeneralEnums;

namespace DrawnIntoDarkness.Cards.EventCards
{
    public sealed class NecroticSporesEvent : EventCardBase
    {
        public override string Name => "Necrotic Spores";
        public override EventScope Scope => EventScope.Combat;
        public override EventTag Tags => EventTag.Trap | EventTag.Curse;
        public override EventTrigger Triggers => EventTrigger.OnBattleStart;

        private int _damage = 1;
        private int _turns = 2;

        private static readonly Dictionary<EventDifficulty, (int damage, int turns)> _tuning
            = new()
            {
                [EventDifficulty.Easy] = (1, 2),
                [EventDifficulty.Normal] = (1, 2),
                [EventDifficulty.Hard] = (2, 3),
                [EventDifficulty.Nightmare] = (3, 3),
            };

        public override void OnActivate(EventContext ctx)
        {
            if (!_tuning.TryGetValue(ctx.Difficulty, out var values))
            {
                values = (1, 2);
            }

            _damage = values.damage;
            _turns = values.turns;
            Description = $"At battle start, necrotic spores Infect everyone for {_turns} turn(s) dealing {_damage} damage per turn.";
        }

        public override void Resolve(EventContext ctx)
        {
            if (ctx.Payload is not BattleStartInfo info)
            {
                return;
            }

            int affected = 0;

            void ApplyTo(IEntity combatant)
            {
                if (combatant is not EntityBase entity || !entity.IsAlive)
                {
                    return;
                }

                entity.ApplyStatus(new InfectStatus(_damage, _turns));
                affected++;
            }

            foreach (var player in info.PlayerEntities)
            {
                ApplyTo(player);
            }

            foreach (var enemy in info.EnemyEntities)
            {
                ApplyTo(enemy);
            }

            ctx.Svc.LogInfo?.Invoke($"Necrotic Spores: afflicted {affected} combatant(s).");
        }
    }
}
