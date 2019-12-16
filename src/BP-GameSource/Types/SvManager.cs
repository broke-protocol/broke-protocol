using BrokeProtocol.Server.LiteDB.Models;
using BrokeProtocol.Utility;

namespace BrokeProtocol.GameSource.Types
{
    public class SvManager
    {
        [Target(typeof(API.Events.Manager), (int)API.Events.Manager.OnStarted)]
        protected void OnStarted(Managers.SvManager svManager)
        {
        }

        [Target(typeof(API.Events.Manager), (int)API.Events.Manager.OnTryLogin)]
        protected void OnTryLogin(Managers.SvManager svManager, AuthData authData, ConnectData connectData)
        {
            if (svManager.settings.steam.spoofID)
            {
                svManager.AuthFail(authData.connection, "SpoofID enabled - Register only");
                return;
            }
            if (!svManager.TryGetUserData(authData.steamID, out User playerData))
            {
                svManager.AuthFail(authData.connection, "Account not found - Please register a new account");
                return;
            }
            if (!svManager.HandleWhitelist(authData.steamID))
            {
                svManager.AuthFail(authData.connection, "You're currently not whitelisted");
                return;
            }
            if (playerData.BanInfo.IsBanned)
            {
                svManager.AuthFail(authData.connection, $"You're currently banned - {playerData.BanInfo.Reason}");
                return;
            }
            svManager.LoadSavedPlayer(playerData, authData, connectData);
        }

        [Target(typeof(API.Events.Manager), (int)API.Events.Manager.OnTryRegister)]
        protected void OnTryRegister(Managers.SvManager svManager, AuthData authData, ConnectData connectData)
        {
            if (svManager.TryGetUserData(authData.steamID, out User playerData) && playerData.BanInfo.IsBanned)
            {
                svManager.AuthFail(authData.connection, $"You're currently banned - {playerData.BanInfo.Reason}");
                return;
            }
            if (!svManager.ValidUsername(connectData.username))
            {
                svManager.AuthFail(authData.connection, "Name cannot be registered (min length: 3, max: 16)");
                return;
            }
            if (!svManager.HandleWhitelist(authData.steamID))
            {
                svManager.AuthFail(authData.connection, "You're currently not whitelisted");
                return;
            }
            svManager.AddNewPlayer(authData, connectData);
        }
    }
}
