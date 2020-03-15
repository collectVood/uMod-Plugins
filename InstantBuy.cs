
namespace Oxide.Plugins
{
    [Info("Instant Buy", "Jake_Rich/collect_vood", "1.0.1")]
    [Description("Vending Machine has no delay")]

    public class InstantBuy : CovalencePlugin
    {
        private object OnBuyVendingItem(VendingMachine machine, BasePlayer player, int sellOrderID, int amount)
        {
            if (machine == null || player == null) return null;

            machine.ClientRPC<int>(null, "CLIENT_StartVendingSounds", sellOrderID);
            machine.DoTransaction(player, sellOrderID, amount);
            return false;
        }
    }
}