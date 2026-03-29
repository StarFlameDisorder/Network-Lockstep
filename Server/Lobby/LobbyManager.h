//
// Created by StarFlame on 2026/3/29.
//

#ifndef SERVER_LOBBYMANAGER_H
#define SERVER_LOBBYMANAGER_H
#include <QObject>
#include "RoomManager.h"
#include "PlayerManager.h"
#include "../protobuf/output/LobbyMessage.pb.h"

class LobbyManager:public QObject
{
    Q_OBJECT
public:
    LobbyManager(QObject* parent=nullptr);
    void handleLobbySync(quint64 clientId,const LobbyMessage::LobbySyncRequest& message);
private:
    RoomManager m_roomManager;
    PlayerManager m_playerManager;
};


#endif //SERVER_LOBBYMANAGER_H