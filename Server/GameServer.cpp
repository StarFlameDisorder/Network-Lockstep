/*
* Created by StarFlame on 2026/2/28.
 * 游戏服务器
 */

#include "GameServer.h"

GameServer::GameServer(QObject* parent)
    :QObject(parent),m_networkDispatcher(this),m_roomManager(this)
{
    connect(&m_networkDispatcher,&NetworkDispatcher::handleTcpLobby,&m_roomManager,&RoomManager::handleLobbySync);

    //局内广播
    connect(&m_networkDispatcher,&NetworkDispatcher::handleUdpGameSync,&m_roomManager,&RoomManager::receiveGameSync);


    connect(&m_roomManager,&RoomManager::sendTcpMessage,&m_networkDispatcher,&NetworkDispatcher::sendTcpMessage);
    connect(&m_roomManager,&RoomManager::sendUdpMessage,&m_networkDispatcher,&NetworkDispatcher::sendUdpMessage);
}
