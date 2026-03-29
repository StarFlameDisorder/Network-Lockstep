//
// Created by StarFlame on 2026/2/28.
//

#ifndef SERVER_GAMESERVER_H
#define SERVER_GAMESERVER_H

#include <QObject>

#include "Network/NetworkDispatcher.h"
#include  "Lobby/LobbyManager.h"

class GameServer:public QObject
{
    Q_OBJECT
public:
    GameServer(QObject* parent=nullptr);
private:
    NetworkDispatcher m_networkDispatcher;
    // LobbyManager m_lobbyManager;
    RoomManager m_roomManager;
};


#endif //SERVER_GAMESERVER_H