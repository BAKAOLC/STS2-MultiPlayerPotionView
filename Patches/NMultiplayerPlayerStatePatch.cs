using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Potions;
using STS2MultiPlayerPotionView.Utils;
using STS2RitsuLib.Patching.Core;
using STS2RitsuLib.Patching.Models;

namespace STS2MultiPlayerPotionView.Patches
{
    public partial class NMultiplayerPlayerStatePatch : IModPatches
    {
        private static readonly Dictionary<NMultiplayerPlayerState, PotionDisplayContainer> PotionContainers = [];

        public static void AddTo(ModPatcher patcher)
        {
            patcher.RegisterPatch<Ready>();
            patcher.RegisterPatch<ExitTree>();
        }

        public static void RefreshAll()
        {
            foreach (var container in PotionContainers.Values.Where(GodotObject.IsInstanceValid))
                container.RefreshPotions();
        }

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
            // ReSharper disable once UnusedMember.Global
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
            // ReSharper disable once UnusedMember.Global
            public static void Postfix(NMultiplayerPlayerState __instance)
            {
                if (!PotionContainers.TryGetValue(__instance, out var container)) return;
                container.Cleanup();
                PotionContainers.Remove(__instance);
            }
        }

        private partial class PotionDisplayContainer : HBoxContainer
        {
            private readonly NMultiplayerPlayerState? _playerState;
            private readonly List<PotionSlotDisplay> _potionSlots = [];
            private PotionDisplayAnchor? _spacer;

            public PotionDisplayContainer(NMultiplayerPlayerState playerState)
            {
                _playerState = playerState;
                Name = "PotionDisplayContainer";
                MouseFilter = MouseFilterEnum.Stop;
                AddThemeConstantOverride("separation", (int)PotionDisplaySettings.GetSeparation());
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
                    CustomMinimumSize = Vector2.Zero,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                _spacer.TransformChanged += UpdatePosition;
                _spacer.Resized += UpdatePosition;

                if (_playerState == null)
                {
                    Main.Logger.Error("Player state is null, cannot initialize potion display");
                    return;
                }

                var topContainer = _playerState.GetNode<HBoxContainer>("TopInfoContainer");
                topContainer.AddChild(_spacer);
                _playerState.AddChild(this);

                var player = _playerState.Player;
                player.PotionProcured += OnPotionChanged;
                player.PotionDiscarded += OnPotionChanged;
                player.UsedPotionRemoved += OnPotionChanged;

                RefreshPotions();
            }

            private void UpdatePosition()
            {
                if (_spacer == null || !_spacer.IsInsideTree() || _playerState == null)
                    return;
                Position = _playerState.GetGlobalTransform().AffineInverse() * _spacer.GlobalPosition +
                           PotionDisplaySettings.GetAutoOffset() + PotionDisplaySettings.GetUserOffset();
            }

            private void RefreshLayout()
            {
                AddThemeConstantOverride("separation", (int)PotionDisplaySettings.GetSeparation());
                CustomMinimumSize = new(
                    PotionDisplaySettings.GetContentWidth(_potionSlots.Count),
                    PotionDisplaySettings.GetContainerHeight());
                _spacer?.CustomMinimumSize = new(CustomMinimumSize.X, 0f);
                Visible = _potionSlots.Count > 0;
                UpdatePosition();
            }

            private void OnPotionChanged(PotionModel _)
            {
                RefreshPotions();
            }

            public void RefreshPotions()
            {
                foreach (var slot in _potionSlots) slot.Cleanup();
                _potionSlots.Clear();

                if (_playerState == null) return;

                var potions = _playerState.Player.PotionSlots;
                foreach (var potion in potions)
                {
                    if (potion == null) continue;
                    var slot = new PotionSlotDisplay(this, _playerState.Player, potion);
                    _potionSlots.Add(slot);
                }

                RefreshLayout();
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

                if (_spacer != null)
                {
                    _spacer.TransformChanged -= UpdatePosition;
                    _spacer.Resized -= UpdatePosition;
                    _spacer.QueueFree();
                }

                QueueFree();
            }
        }

        private partial class PotionDisplayAnchor : Control
        {
            public event Action? TransformChanged;

            public PotionDisplayAnchor()
            {
                SetNotifyTransform(true);
            }

            public override void _Notification(int what)
            {
                if (what == NotificationTransformChanged)
                    TransformChanged?.Invoke();
            }
        }

        private class PotionSlotDisplay
        {
            private readonly Player _player;
            private readonly PotionModel _potion;
            private readonly NPotionHolder _potionHolder;
            private readonly Control _slotControl;
            private NHoverTipSet? _positionedHoverTipSet;

            public PotionSlotDisplay(Control parent, Player player, PotionModel potion)
            {
                _player = player;
                _potion = potion;

                _slotControl = new()
                {
                    CustomMinimumSize = PotionDisplaySettings.GetSlotSize(),
                    Size = PotionDisplaySettings.GetSlotSize(),
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                    MouseFilter = Control.MouseFilterEnum.Stop,
                };

                parent.AddChild(_slotControl);

                _potionHolder = NPotionHolder.Create(isUsable: false);
                _slotControl.AddChild(_potionHolder);
                _potionHolder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
                _potionHolder.PivotOffset = Vector2.Zero;
                _potionHolder.Scale = PotionDisplaySettings.GetSlotSize() / _potionHolder.Size;
                _potionHolder.Position = Vector2.Zero;

                var potionNode = NPotion.Create(_potion)!;
                _potionHolder.AddPotion(potionNode);
                potionNode.Position = Vector2.Zero;

                if (PotionDisplaySettings.TryGetHighlightColor(_potion, out var color))
                {
                    var highlightBorder = new Panel
                    {
                        Visible = true,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    };
                    highlightBorder.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                    highlightBorder.AddThemeStyleboxOverride("panel", CreateHighlightStyle(color));
                    _slotControl.AddChild(highlightBorder);
                }

                _potionHolder.GuiInput += OnGuiInput;
                _potionHolder.MouseEntered += RepositionHoverTip;
                _potionHolder.FocusEntered += RepositionHoverTip;
            }

            private void RepositionHoverTip()
            {
                if (!NHoverTipSet._activeHoverTips.TryGetValue(_potionHolder, out var hoverTipSet) ||
                    hoverTipSet == _positionedHoverTipSet)
                    return;

                var nativeDistance = _potionHolder.Size.Y * Mathf.Max(1.5f, _potionHolder.Scale.Y);
                var displayDistance = _potionHolder.GetGlobalRect().Size.Y * 1.5f;
                hoverTipSet.GlobalPosition += Vector2.Down * (displayDistance - nativeDistance);
                hoverTipSet.SetAlignment(_potionHolder, HoverTipAlignment.None);
                _positionedHoverTipSet = hoverTipSet;
            }

            private void OnGuiInput(InputEvent @event)
            {
                if (@event is not InputEventMouseButton
                    {
                        Pressed: true,
                        AltPressed: true,
                        ButtonIndex: var buttonIndex,
                    })
                    return;

                var handled = false;

                if (buttonIndex is MouseButton.Left or MouseButton.Right)
                    handled |= LemonSpireInterop.TrySendPotionToChat(_player, _potion);

                if (buttonIndex == MouseButton.Left)
                    handled |= TypingInterop.TrySendPotionLink(_potion);

                if (!handled) return;

                _slotControl.GetViewport()?.SetInputAsHandled();
            }

            public void Cleanup()
            {
                _potionHolder.GuiInput -= OnGuiInput;
                _potionHolder.MouseEntered -= RepositionHoverTip;
                _potionHolder.FocusEntered -= RepositionHoverTip;
                NHoverTipSet.Remove(_potionHolder);
                _slotControl.QueueFree();
            }

            private static StyleBoxFlat CreateHighlightStyle(Color borderColor)
            {
                return new()
                {
                    DrawCenter = false,
                    BorderColor = borderColor,
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
