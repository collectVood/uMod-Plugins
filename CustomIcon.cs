namespace Oxide.Plugins
{
    [Info("Custom Icon", "collect_vood", "0.0.1")]
    [Description("Set a customizable icon for all server/plugin messages")]

    class CustomIcon : RustPlugin
    {
        private void SendMessage(BasePlayer player, string message) =>
            player.SendConsoleCommand("chat.add", 76561198965214956, message);

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            Puts("OnServerCommand works!");
            return null;
        }
        private object OnServerMessage(string message, string name)
        {
            if (message.Contains("gave") && name == "SERVER")
            {
                foreach (BasePlayer player in Player.Players)
                    SendMessage(player, name + " " + message);
                
                return true;
            }
            return null;
        }
        object OnPlayerCommand(ConsoleSystem.Arg arg)
        {
            Puts("OnPlayerCommand works!");
            return null;
        }
        object OnUserCommand(BasePlayer player)
        {
            Puts("OnUserCommand works");
            return null;
        }
        object OnMessagePlayer(string message, BasePlayer player)
        {
            Puts("OnMessagePlayer works!");

            SendMessage(player, message);
            return true;
        }
        [ChatCommand("t")]
        void TestCommand(BasePlayer player, string cmd, string[] args)
        {
            player.ChatMessage("Test!");
            player.IPlayer.Reply("2Test!");
            Server.Broadcast("A server broadcast");
        }
    }
}