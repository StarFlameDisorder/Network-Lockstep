//
// Created by StarFlame on 2026/2/28.
//

#include "GameServer.h"

GameServer::GameServer(QObject* parent)
    :QObject(parent),m_networkDispatcher(this),m_lobbyManager(this)
{
    connect(&m_networkDispatcher,&NetworkDispatcher::handleTcpLobby,&m_lobbyManager,&LobbyManager::handleLobbySync);
}
