/*
 * Created by StarFlame on 2026/2/28.
 * 游戏服务器
 */

#ifndef SERVER_GAMESERVER_H
#define SERVER_GAMESERVER_H

#include <QObject>
#include <QThread>

#include "Network/NetworkDispatcher.h"
#include  "Lobby/RoomManager.h"

class GameServer:public QObject
{
    Q_OBJECT
public:
    GameServer(QObject* parent=nullptr);
    ~GameServer();
private:
    NetworkDispatcher *m_networkDispatcher;
    RoomManager m_roomManager;


    QThread m_networkThread;//网络线程
};


#endif //SERVER_GAMESERVER_H