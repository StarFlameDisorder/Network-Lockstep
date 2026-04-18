/*
* Created by StarFlame on 2026/2/28.
 * 游戏服务器
 */

#include "GameServer.h"

GameServer::GameServer(QObject* parent)
    :QObject(parent),m_networkDispatcher(this),m_roomManager(this)
{
    connect(&m_networkDispatcher,&NetworkDispatcher::handleTcpLobby,&m_roomManager,&RoomManager::handleLobbySync);

    //断线处理
    connect(&m_networkDispatcher,&NetworkDispatcher::clientDisconnectRequest,&m_roomManager,&RoomManager::receiveClientDisconnection);
    connect(&m_roomManager,&RoomManager::removeClient,&m_networkDispatcher,&NetworkDispatcher::deleteClient);

    //局内Udp
    connect(&m_networkDispatcher,&NetworkDispatcher::handleUdpGameSync,&m_roomManager,&RoomManager::receiveGameSync);
    connect(&m_networkDispatcher,&NetworkDispatcher::handleUdpHeartBeat,&m_roomManager,&RoomManager::receiveHeartBeat);

    connect(&m_roomManager,&RoomManager::sendTcpMessage,&m_networkDispatcher,&NetworkDispatcher::sendTcpMessage);
    connect(&m_roomManager,&RoomManager::sendUdpMessage,&m_networkDispatcher,&NetworkDispatcher::sendUdpMessage);
}
