using BrokeProtocol.Server.LiteDB.Models;
using BrokeProtocol.Utility;
using BrokeProtocol.Managers;
using BrokeProtocol.Entities;
using BrokeProtocol.Collections;
using BrokeProtocol.API;


namespace BrokeProtocol.GameSource.Types
{
    public class Manager
    {
        //[Target(GameSourceEvent.ManagerStart, ExecutionMode.Override)]
        //public void OnStart(SvManager svManager) { }

        //[Target(GameSourceEvent.ManagerUpdate, ExecutionMode.Override)]
        //public void OnUpdate(SvManager svManager) { }

        //[Target(GameSourceEvent.ManagerFixedUpdate, ExecutionMode.Override)]
        //public void OnFixedUpdate(SvManager svManager) { }

        //[Target(GameSourceEvent.ManagerConsoleInput, ExecutionMode.Override)]
        //public void OnConsoleInput(SvManager svManager, string cmd) { }

        [Target(GameSourceEvent.ManagerTryLogin, ExecutionMode.Override)]
        public void OnTryLogin(SvManager svManager, AuthData authData, ConnectData connectData)
        {
            if (ValidateUser(svManager, authData))
            {
                if (!svManager.TryGetUserData(authData.accountID, out User playerData))
                {
                    svManager.RegisterFail(authData.connection, "Account not found - Please Register");
                    return;
                }

                if (playerData.PasswordHash != connectData.passwordHash)
                {
                    svManager.RegisterFail(authData.connection, $"Invalid credentials");
                    return;
                }

                svManager.LoadSavedPlayer(playerData, authData, connectData);
            }
        }

        [Target(GameSourceEvent.ManagerTryRegister, ExecutionMode.Override)]
        public void OnTryRegister(SvManager svManager, AuthData authData, ConnectData connectData)
        {
            if (ValidateUser(svManager, authData))
            {
                if (svManager.TryGetUserData(authData.accountID, out User playerData))
                {
                    if (playerData.PasswordHash != connectData.passwordHash)
                    {
                        svManager.RegisterFail(authData.connection, $"Invalid credentials");
                        return;
                    }
                }

                if (!connectData.username.ValidCredential())
                {
                    svManager.RegisterFail(authData.connection, $"Name cannot be registered (min: {Util.minCredential}, max: {Util.maxCredential})");
                    return;
                }

                svManager.AddNewPlayer(authData, connectData);
            }
        }

        [Target(GameSourceEvent.ManagerSave, ExecutionMode.Override)]
        public void OnSave(SvManager svManager)
        {
            ChatHandler.SendToAll("Saving server status..");
            foreach (ShPlayer player in EntityCollections.Humans)
            {
                player.svPlayer.Save();
            }
            svManager.database.WriteOut();
        }

        private bool ValidateUser(SvManager svManager, AuthData authData)
        {
            if (!svManager.HandleWhitelist(authData.accountID))
            {
                svManager.RegisterFail(authData.connection, "Account not whitelisted");
                return false;
            }

            // Don't allow multi-boxing, WebAPI doesn't prevent this
            foreach (ShPlayer p in EntityCollections.Humans)
            {
                if (p.accountID == authData.accountID)
                {
                    svManager.RegisterFail(authData.connection, "Account still logged in");
                    return false;
                }
            }

            return true;
        }
    }
}
