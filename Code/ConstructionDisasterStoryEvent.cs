using System;
using Game.Commands;
using Game.Components;
using Game.Constants;
using Game.Data;
using Game.Story.Events;
using Game.UI;
using Game.Utils;
using KL.Utils;
using UnityEngine;

namespace ConstructionDisaster {
    public sealed class ConstructionDisasterStoryEvent : StoryEvent {
        public const string Id = "ConstructionDisaster";

        private static readonly StoryEventMeta meta = new StoryEventMeta {
            Id = Id,
            AutoEnd = false,
            SkipDurationWarning = true,
            Create = (() => new ConstructionDisasterStoryEvent())
        };
        private string evTitle;
        private string evDesc;

        public override StoryEventMeta Meta => meta;

        private EventLogEntry logEntry;
        public override EventLogEntry LogEntry => logEntry;
        private EventNotification notification;
        private EventLogEntry BuildLogEntry(Tile tile) {
            evTitle = "ev.construct.fail.title".T();
            evDesc = "ev.construct.fail.desc".T(tile.Definition.NameT);
            return EventLogEntry.CreateFor(this,
                evTitle, evDesc, IconId.CDeconstruct, tile.Id);
        }
        private void NotifyFailure(Tile tile) {
            logEntry = BuildLogEntry(tile);
            S.Story.Log.AddLogEntry(logEntry);
            notification = EventNotification.Create(
                S.Ticks, UDB.Create(tile.MainDataProvider,
                    UDBT.IEvent, IconId.CDeconstruct, evTitle)
                .WithIconClickFunction(LogEntry.ShowDetails),
                    Priority.Normal, true);
            S.Sig.AddEvent.Send(this.notification);
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register() {
            StoryEvent.Register(ConstructionDisasterStoryEvent.meta);
        }

        public override void OnStart(long ticks) {
            /*
            UIPopupWidget.Spawn(IconId.CInfo, "Construction Disaster Test",
                "Story event starting now, it works");
            D.Warn("Story event works! construction");
            */
            StartListeningToConstruction();
        }

        public override void OnEnd(long ticks) {
            if (isListeningToConstructionCompleted) {
                isListeningToConstructionCompleted = false;
                S.Sig.ConstructionCompleted.RemoveListener(OnConstructionCompleted);
            }
        }

        protected override void OnLoad(ComponentData data) {
            StartListeningToConstruction();
        }

        private bool isListeningToConstructionCompleted;

        public override void OnTick(long ticks) { }

        private void StartListeningToConstruction() {
            if (!isListeningToConstructionCompleted) {
                isListeningToConstructionCompleted = true;
                S.Sig.ConstructionCompleted.AddListener(OnConstructionCompleted);
            }
        }

        private void OnConstructionCompleted(Tile tile) {
            if (tile.EnergyNode == null) { return; }
            try {
                var rng = S.Rng.Fork();
                var dmgPct = rng.Range(0.1f, 0.3f) * S.Difficulty.StoryEventDifficulty;
                NotifyFailure(tile);
                // First deal some damage
                tile.Damageable.TakeDamage(tile.Damageable.MaxHealth * dmgPct,
                    DamageType.Electricity);
                bool canExplode = !S.Prefs.GetBool(Pref.NoHeavyDisasters, false);
                // Difficulty can go from 0.5 (Relaxing) to 2.0 (Challenging)
                var explodeChance = Mathf.Lerp(0f, 0.4f,
                    S.Difficulty.StoryEventDifficulty * 0.5f);
                if (canExplode && rng.Chance(explodeChance)) {
                    // explosion
                    var radius = rng.Range(1f, 3f) * S.Difficulty.StoryEventDifficulty;
                    var str = rng.Range(50f, 100f) * S.Difficulty.StoryEventDifficulty;
                    // Deal some damage
                    var cmd = new CmdCreateExplosion(tile.Position, radius, str, rng: rng);
                    S.CmdQ.Enqueue(cmd);
                } else {
                    // start a fire
                    tile.Flammable.SetOnFire();
                }
            } catch (Exception ex) {
                D.LogEx(ex, "Failed to execute ConstructionDisaster on {0}", tile);
            }
            EndEvent();
        }
    }
}