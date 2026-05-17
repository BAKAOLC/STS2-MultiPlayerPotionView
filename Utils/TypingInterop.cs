using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace STS2MultiPlayerPotionView.Utils
{
    internal static class TypingInterop
    {
        private const string ChatItemLinkTypeName = "Typing.ChatItemLink";
        private const string ChatPanelTypeName = "Typing.ChatPanel";

        private static readonly Lazy<Func<PotionModel, string>?> EncodePotion = new(CreateEncodePotion);
        private static readonly Lazy<Action<string>?> SendText = new(CreateSendText);

        public static bool TrySendPotionLink(PotionModel potion)
        {
            var encode = EncodePotion.Value;
            var send = SendText.Value;
            if (encode == null || send == null) return false;

            try
            {
                send(encode(potion));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Func<PotionModel, string>? CreateEncodePotion()
        {
            var type = ResolveTypingType(ChatItemLinkTypeName);
            var method = type?.GetMethod(
                "EncodePotion",
                BindingFlags.Public | BindingFlags.Static,
                null,
                [typeof(PotionModel)],
                null);
            return method?.CreateDelegate<Func<PotionModel, string>>();
        }

        private static Action<string>? CreateSendText()
        {
            var panelType = ResolveTypingType(ChatPanelTypeName);
            var method = panelType?.GetMethod(
                "SendText",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                [typeof(string)],
                null);
            if (method == null) return null;

            return text =>
            {
                var panel = FindChatPanel();
                if (panel == null) return;
                method.Invoke(panel, [text]);
            };
        }

        private static object? FindChatPanel()
        {
            var game = NGame.Instance;
            return game == null ? null : FindChildByTypeName(game, ChatPanelTypeName);
        }

        private static Node? FindChildByTypeName(Node root, string typeFullName)
        {
            if (root.GetType().FullName == typeFullName)
                return root;

            return root.GetChildren().Select(child => FindChildByTypeName(child, typeFullName))
                .OfType<Node>().FirstOrDefault();
        }

        private static Type? ResolveTypingType(string typeName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(typeName, false))
                .FirstOrDefault(type => type != null);
        }
    }
}
