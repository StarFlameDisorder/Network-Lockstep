//
// Created by StarFlame on 2026/2/28.
//

#ifndef SERVER_GAMESERVER_H
#define SERVER_GAMESERVER_H

#include <QObject>

#include "Lobby/PlayerManager.h"
#include "Lobby/RoomManager.h"
#include "Network/NetworkDispatcher.h"

class GameServer:public QObject
{
public:
    GameServer(QObject* parent=nullptr);
private:
    NetworkDispatcher m_networkDispatcher;
    RoomManager m_roomManager;
    PlayerManager m_playerManager;
};


#endif //SERVER_GAMESERVER_H