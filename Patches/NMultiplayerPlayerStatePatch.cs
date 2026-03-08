using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using STS2MultiPlayerPotionView.Patching.Core;
using STS2MultiPlayerPotionView.Patching.Models;

namespace STS2MultiPlayerPotionView.Patches
{
    /// <summary>
    ///     Patches for NMultiplayerPlayerState to display player potions
    /// </summary>
    public class NMultiplayerPlayerStatePatch : IModPatches
    {
        private static readonly Dictionary<NMultiplayerPlayerState, PotionDisplayContainer> PotionContainers = [];

        public static void AddTo(ModPatcher patcher)
        {
            patcher.RegisterPatch<Ready>();
            patcher.RegisterPatch<ExitTree>();
        }

        /// <summary>
        ///     Patch for _Ready method
        /// </summary>
        public class Ready : IPatchMethod
        {
            public static string PatchId => "multiplayer_player_state_ready";
            public static string Description => "Initialize potion display in multiplayer player state";

            public static ModPatchTarget[] GetTargets()
            {
                return
                [
                    new(typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._Ready)),
                ];
            }

            // ReSharper disable once InconsistentNaming
            public static void Postfix(NMultiplayerPlayerState __instance)
            {
                try
                {
                    var container = new PotionDisplayContainer(__instance);
                    PotionContainers[__instance] = container;
                    container.Initialize();
                }
                catch (Exception ex)
                {
                    Main.Logger.Error($"Failed to initialize potion display: {ex.Message}");
                }
            }
        }

        /// <summary>
        ///     Patch for _ExitTree method
        /// </summary>
        public class ExitTree : IPatchMethod
        {
            public static string PatchId => "multiplayer_player_state_exit_tree";
            public static string Description => "Cleanup potion display in multiplayer player state";

            public static ModPatchTarget[] GetTargets()
            {
                return
                [
                    new(typeof(NMultiplayerPlayerState), nameof(NMultiplayerPlayerState._ExitTree)),
                ];
            }

            // ReSharper disable once InconsistentNaming
            public static void Postfix(NMultiplayerPlayerState __instance)
            {
                if (!PotionContainers.TryGetValue(__instance, out var container)) return;
                container.Cleanup();
                PotionContainers.Remove(__instance);
            }
        }

        public class PotionDisplayContainer(NMultiplayerPlayerState playerState)
        {
            private readonly List<PotionSlotDisplay> _potionSlots = [];
            private HBoxContainer? _potionContainer;

            public void Initialize()
            {
                _potionContainer = new()
                {
                    Name = "PotionDisplayContainer",
                    CustomMinimumSize = new(0, 32),
                };
                _potionContainer.AddThemeConstantOverride("separation", 2);
                _potionContainer.ZIndex = 10;

                var topContainer = playerState.GetNode<HBoxContainer>("TopInfoContainer");
                topContainer.AddChild(_potionContainer);

                var player = playerState.Player;
                player.PotionProcured += OnPotionChanged;
                player.PotionDiscarded += OnPotionChanged;
                player.UsedPotionRemoved += OnPotionChanged;

                RefreshPotions();
            }

            public void Cleanup()
            {
                var player = playerState.Player;
                player.PotionProcured -= OnPotionChanged;
                player.PotionDiscarded -= OnPotionChanged;
                player.UsedPotionRemoved -= OnPotionChanged;

                foreach (var slot in _potionSlots) slot.Cleanup();

                _potionSlots.Clear();
                _potionContainer?.QueueFree();
            }

            private void OnPotionChanged(PotionModel _)
            {
                RefreshPotions();
            }

            private void RefreshPotions()
            {
                foreach (var slot in _potionSlots) slot.Cleanup();

                _potionSlots.Clear();

                var potions = playerState.Player.PotionSlots;
                foreach (var potion in potions)
                {
                    if (potion == null) continue;
                    var slot = new PotionSlotDisplay(_potionContainer!, potion);
                    _potionSlots.Add(slot);
                }
            }
        }

        private class PotionSlotDisplay
        {
            private readonly PotionModel _potion;
            private readonly Control _slotControl;
            private NHoverTipSet? _hoverTipSet;

            public PotionSlotDisplay(Control parent, PotionModel potion)
            {
                _potion = potion;

                _slotControl = new()
                {
                    CustomMinimumSize = new(24, 24),
                    MouseFilter = Control.MouseFilterEnum.Pass,
                };

                TextureRect potionImage = new()
                {
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };

                _slotControl.AddChild(potionImage);
                potionImage.SetAnchorsPreset(Control.LayoutPreset.FullRect);

                parent.AddChild(_slotControl);

                potionImage.Texture = _potion.Image;
                potionImage.SelfModulate = Colors.White;

                _slotControl.MouseEntered += OnMouseEntered;
                _slotControl.MouseExited += OnMouseExited;
            }

            private void OnMouseEntered()
            {
                try
                {
                    _hoverTipSet = NHoverTipSet.CreateAndShow(_slotControl, _potion.HoverTips.ToArray());
                    _hoverTipSet?.GlobalPosition = _slotControl.GlobalPosition + Vector2.Down * 40f;
                }
                catch (Exception ex)
                {
                    Main.Logger.Error($"Failed to show potion hover tip: {ex.Message}");
                }
            }

            private void OnMouseExited()
            {
                if (_hoverTipSet == null) return;
                NHoverTipSet.Remove(_slotControl);
                _hoverTipSet = null;
            }

            public void Cleanup()
            {
                _slotControl.MouseEntered -= OnMouseEntered;
                _slotControl.MouseExited -= OnMouseExited;

                NHoverTipSet.Remove(_slotControl);
                _slotControl.QueueFree();
            }
        }
    }
}
