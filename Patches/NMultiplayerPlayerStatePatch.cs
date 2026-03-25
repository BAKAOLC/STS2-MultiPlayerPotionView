using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;
using STS2MultiPlayerPotionView.Utils;

namespace STS2MultiPlayerPotionView.Patches
{
    /// <summary>
    ///     Patches for NMultiplayerPlayerState to display player potions
    /// </summary>
    public partial class NMultiplayerPlayerStatePatch : IModPatches
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

        // ReSharper disable once PartialTypeWithSinglePart
        private partial class PotionDisplayContainer : HBoxContainer
        {
            private readonly NMultiplayerPlayerState? _playerState;
            private readonly List<PotionSlotDisplay> _potionSlots = [];
            private Control? _spacer;

            public PotionDisplayContainer(NMultiplayerPlayerState playerState)
            {
                _playerState = playerState;
                Name = "PotionDisplayContainer";
                CustomMinimumSize = new(0, PotionDisplaySettings.GetSlotSize().Y + 8f);
                MouseFilter = MouseFilterEnum.Stop;
                AddThemeConstantOverride("separation", 2);
            }

            // ReSharper disable once UnusedMember.Local
            public PotionDisplayContainer()
            {
                _playerState = null;
            }

            public void Initialize()
            {
                _spacer = new()
                {
                    Name = "PotionSpacer",
                    CustomMinimumSize = new(0, 0),
                    MouseFilter = MouseFilterEnum.Ignore,
                };

                if (_playerState == null)
                {
                    Main.Logger.Error("Player state is null, cannot initialize potion display");
                    return;
                }

                var topContainer = _playerState.GetNode<HBoxContainer>("TopInfoContainer");
                topContainer.AddChild(_spacer);

                _playerState.AddChild(this);

                Resized += OnResized;

                var player = _playerState.Player;
                player.PotionProcured += OnPotionChanged;
                player.PotionDiscarded += OnPotionChanged;
                player.UsedPotionRemoved += OnPotionChanged;

                RefreshPotions();
            }

            public override void _Process(double delta)
            {
                if (_spacer != null && _spacer.IsInsideTree()) GlobalPosition = _spacer.GlobalPosition;
            }

            private void OnResized()
            {
                _spacer?.CustomMinimumSize = new(Size.X, 0);
            }

            private void OnPotionChanged(PotionModel _)
            {
                RefreshPotions();
            }

            private void RefreshPotions()
            {
                foreach (var slot in _potionSlots) slot.Cleanup();
                _potionSlots.Clear();

                if (_playerState == null) return;

                var potions = _playerState.Player.PotionSlots;
                foreach (var potion in potions)
                {
                    if (potion == null) continue;
                    var slot = new PotionSlotDisplay(this, potion);
                    _potionSlots.Add(slot);
                }
            }

            public void Cleanup()
            {
                if (_playerState == null) return;

                var player = _playerState.Player;
                player.PotionProcured -= OnPotionChanged;
                player.PotionDiscarded -= OnPotionChanged;
                player.UsedPotionRemoved -= OnPotionChanged;

                foreach (var slot in _potionSlots) slot.Cleanup();
                _potionSlots.Clear();

                Resized -= OnResized;

                _spacer?.QueueFree();
                QueueFree();
            }
        }

        private class PotionSlotDisplay
        {
            private readonly PotionModel _potion;
            private readonly Panel _highlightBorder;
            private readonly Control _slotControl;
            private NHoverTipSet? _hoverTipSet;

            public PotionSlotDisplay(Control parent, PotionModel potion)
            {
                _potion = potion;

                _slotControl = new()
                {
                    CustomMinimumSize = PotionDisplaySettings.GetSlotSize(),
                    MouseFilter = Control.MouseFilterEnum.Stop,
                };

                TextureRect potionImage = new()
                {
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };

                _slotControl.AddChild(potionImage);
                potionImage.SetAnchorsPreset(Control.LayoutPreset.FullRect);

                _highlightBorder = new Panel
                {
                    Visible = PotionDisplaySettings.ShouldHighlight(_potion),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                _highlightBorder.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                _highlightBorder.AddThemeStyleboxOverride("panel", CreateHighlightStyle());
                _slotControl.AddChild(_highlightBorder);

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

            private static StyleBoxFlat CreateHighlightStyle()
            {
                return new()
                {
                    DrawCenter = false,
                    BorderColor = PotionDisplaySettings.GetHighlightColor(),
                    BorderWidthLeft = 2,
                    BorderWidthTop = 2,
                    BorderWidthRight = 2,
                    BorderWidthBottom = 2,
                    CornerRadiusTopLeft = 6,
                    CornerRadiusTopRight = 6,
                    CornerRadiusBottomLeft = 6,
                    CornerRadiusBottomRight = 6,
                };
            }
        }
    }
}
