using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2MultiPlayerPotionView.Utils
{
    internal static class LemonSpireInterop
    {
        private const string HelperTypeName = "lemonSpire2.PlayerStateEx.PanelProvider.PlayerPanelChatHelper";
        private const string PotionShareLocEntryKey = "LEMONSPIRE.chat.potionShare";
        private static readonly Lazy<PotionOperation?> SendPotionToChat = new(CreateSendPotionToChat);

        public static bool TrySendPotionToChat(Player player, PotionModel potion)
        {
            return TryInvoke(SendPotionToChat.Value, player, potion);
        }

        private static bool TryInvoke(PotionOperation? operation, Player player, PotionModel potion)
        {
            if (operation == null) return false;

            try
            {
                return operation(player, potion);
            }
            catch
            {
                return false;
            }
        }

        private static PotionOperation? CreateSendPotionToChat()
        {
            var helperType = ResolveHelperType();
            var method = helperType?.GetMethod(
                "SendPotionToChat",
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(Player), typeof(string), typeof(PotionModel)],
                null);
            if (method == null) return null;

            var send = method.CreateDelegate<Action<Player, string, PotionModel>>();
            return (player, potion) =>
            {
                send(player, PotionShareLocEntryKey, potion);
                return true;
            };
        }

        private static Type? ResolveHelperType()
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(HelperTypeName, false))
                .FirstOrDefault(type => type != null);
        }

        private delegate bool PotionOperation(Player player, PotionModel potion);
    }
}
