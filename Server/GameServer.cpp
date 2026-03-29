//
// Created by StarFlame on 2026/2/28.
//

#include "GameServer.h"

GameServer::GameServer(QObject* parent)
    :QObject(parent),m_networkDispatcher(this),m_roomManager(this)
{
    connect(&m_networkDispatcher,&NetworkDispatcher::handleTcpLobby,&m_roomManager,&RoomManager::handleLobbySync);
    connect(&m_roomManager,&RoomManager::sendTcpMessage,&m_networkDispatcher,&NetworkDispatcher::sendTcpMessage);
}
