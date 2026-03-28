//
// Created by StarFlame on 2026/2/28.
//

#ifndef SERVER_GAMESERVER_H
#define SERVER_GAMESERVER_H

#include "Network/NetworkDispatcher.h"

class GameServer
{
public:
    GameServer();
private:
    NetworkDispatcher m_networkDispatcher;
};


#endif //SERVER_GAMESERVER_H