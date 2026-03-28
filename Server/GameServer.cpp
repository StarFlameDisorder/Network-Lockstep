//
// Created by StarFlame on 2026/2/28.
//

#include "GameServer.h"

GameServer::GameServer(QObject* parent)
    :QObject(parent),m_networkDispatcher(this),m_playerManager(this),m_roomManager(this)
{
}
