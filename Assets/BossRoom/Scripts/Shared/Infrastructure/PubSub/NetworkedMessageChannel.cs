using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Unity.Multiplayer.Samples.BossRoom.Shared.Infrastructure
{
    /// <summary>
    /// This type of message channel allows the server to publish a message that will be sent to clients as well as
    /// being published locally. Clients and the server both can subscribe to it, but it needs to be done after
    /// the NetworkManager is initialized.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class NetworkedMessageChannel<T> : MessageChannel<T> where T : unmanaged
    {
        string m_Name;

        int m_BufferSize;

        bool m_HasRegisteredHandler;

        public NetworkedMessageChannel(string name, int bufferSize)
        {
            m_Name = name;
            m_BufferSize = bufferSize;
        }

        ~NetworkedMessageChannel()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.CustomMessagingManager != null)
            {
                NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(m_Name);
            }
        }

        public override IDisposable Subscribe(Action<T> handler)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (!m_HasRegisteredHandler && NetworkManager.Singleton.IsClient)
                {
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(m_Name, ReceiveMessageThroughNetwork);
                    m_HasRegisteredHandler = true;
                    NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
                }

                return base.Subscribe(handler);
            }

            Debug.LogError("Cannot subscribe to NetworkedMessageChannel. NetworkManager is not initialized.");
            return null;
        }

        void OnClientDisconnect(ulong clientId)
        {
            m_HasRegisteredHandler = false;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientDisconnect;
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(m_Name);
        }

        public override void Publish(T message)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                // send message to clients, then publish locally
                SendMessageThroughNetwork(message);
                base.Publish(message);
            }
            else
            {
                Debug.LogError("Only a server can publish in a NetworkedMessageChannel");
            }
        }

        void SendMessageThroughNetwork(T message)
        {
            var writer = new FastBufferWriter(m_BufferSize, Allocator.Temp);
            writer.WriteValueSafe(message);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(m_Name, writer);
        }

        void ReceiveMessageThroughNetwork(ulong clientID, FastBufferReader reader)
        {
            reader.ReadValueSafe(out T message);
            base.Publish(message);
        }
    }
}
