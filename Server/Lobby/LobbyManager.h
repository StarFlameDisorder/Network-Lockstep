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
    void addPlayer(quint64 clientId,const LobbyMessage::PlayerLoginRequest& message);

    RoomManager m_roomManager;
    PlayerManager m_playerManager;
signals:
    void sendTcpMessage(quint64 clientId,const QByteArray &message);
    void sendUdpMessage(quint64 clientId,const QByteArray &message);
};


#endif //SERVER_LOBBYMANAGER_H