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
        public void OnTryLogin(SvManager svManager, ConnectionData connectionData)
        {
            if (ValidateUser(svManager, connectionData))
            {
                if (!svManager.TryGetUserData(connectionData.username, out User playerData))
                {
                    svManager.RegisterFail(connectionData.connection, "Account not found - Please Register");
                    return;
                }

                if (playerData.PasswordHash != connectionData.passwordHash)
                {
                    svManager.RegisterFail(connectionData.connection, "Invalid credentials");
                    return;
                }

                svManager.LoadSavedPlayer(playerData, connectionData);
            }
        }

        [Target(GameSourceEvent.ManagerTryRegister, ExecutionMode.Override)]
        public void OnTryRegister(SvManager svManager, ConnectionData connectionData)
        {
            if (ValidateUser(svManager, connectionData))
            {
                if (svManager.TryGetUserData(connectionData.username, out User playerData))
                {
                    if (playerData.PasswordHash != connectionData.passwordHash)
                    {
                        svManager.RegisterFail(connectionData.connection, "Invalid credentials");
                        return;
                    }
                }

                if (!connectionData.username.ValidCredential())
                {
                    svManager.RegisterFail(connectionData.connection, $"Name cannot be registered (min: {Util.minCredential}, max: {Util.maxCredential})");
                    return;
                }

                svManager.AddNewPlayer(connectionData);
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

        private bool ValidateUser(SvManager svManager, ConnectionData connectionData)
        {
            if (!svManager.HandleWhitelist(connectionData.username))
            {
                svManager.RegisterFail(connectionData.connection, "Account not whitelisted");
                return false;
            }

            // Don't allow multi-boxing, WebAPI doesn't prevent this
            foreach (ShPlayer p in EntityCollections.Humans)
            {
                if (p.username == connectionData.username)
                {
                    svManager.RegisterFail(connectionData.connection, "Account still logged in");
                    return false;
                }
            }

            return true;
        }
    }
}
