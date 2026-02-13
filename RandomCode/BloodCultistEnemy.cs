using DrawnIntoDarkness.Cards;
using DrawnIntoDarkness.Cards.ActionCards;
using DrawnIntoDarkness.Cards.ActionCards.StatusEffect;
using DrawnIntoDarkness.Cards.AttackModifierCards;
using DrawnIntoDarkness.Cards.DefenseModifierCards;
using DrawnIntoDarkness.Managers;
using System.Linq;

namespace DrawnIntoDarkness.Entities.Enemies
{
    public sealed class BloodCultistEnemy : EnemyEntity
    {
        public BloodCultistEnemy()
            : base(
                "Blood Cultist",
                new DeckManager(581, 582, 583),
                new EnemyAI { TakeTurn = (enemy, ctx, scheduler) => EnemyAIRuntime.PlayFromActionDeck(enemy, ctx, scheduler, 3) },
                maxHp: 16)
        {
            PowerLevel = 73.27;
        }

        private BloodCultistEnemy(string name, DeckManager decks, EnemyAI ai, int maxHp)
            : base(name, decks, ai, maxHp)
        {
            PowerLevel = 73.27;
        }

        public static BloodCultistEnemy CreateBasic(ulong seedBase = 580)
        {
            var decks = new DeckManager(seedBase + 1, seedBase + 2, seedBase + 3);

            decks.AttackDeck.Load(new ICard[]
            {
                AttackModifierCard.Hit(2),
                AttackModifierCard.Combo(2),
                AttackModifierCard.Crit(2),
                AttackModifierCard.Hit(1)
            });

            decks.DefenseDeck.Load(new ICard[]
            {
                DefenseModifierCard.Block(1),
                DefenseModifierCard.Block(2),
                DefenseModifierCard.Evade(),
                DefenseModifierCard.Chain(1)
            });

            decks.ActionDeck.Load(new ICard[]
            {
                new LeechStrike(),
                new Infect(),
                new CripplingBlow(),
                new LeechStrike()
            });

            var ai = new EnemyAI
            {
                TakeTurn = (enemy, ctx, scheduler) =>
                {
                    bool hasLifesteal = enemy.Statuses.OfType<LifestealStatus>().Any();
                    if (!hasLifesteal)
                    {
                        EnemyAIRuntime.PlayFromActionDeck(enemy, ctx, scheduler, maxAttempts: 4);
                    }

                    EnemyAIRuntime.PlayFromActionDeck(enemy, ctx, scheduler, maxAttempts: 3);

                    if (ctx.Enemies.Count > 1)
                    {
                        EnemyAIRuntime.PlayFromActionDeck(enemy, ctx, scheduler, maxAttempts: 2);
                    }
                }
            };

            return new BloodCultistEnemy("Blood Cultist", decks, ai, maxHp: 16);
        }
    }
}
