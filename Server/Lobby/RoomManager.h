//
// Created by StarFlame on 2026/3/28.
//

#ifndef SERVER_ROOMMANAGER_H
#define SERVER_ROOMMANAGER_H


#include <QObject>
#include <QHash>
#include "../protobuf/output/SyncMessage.pb.h"

// struct Player{
//     Player(quint64 playerId,QString playerName):id{playerId},name(playerName)
//     {}
//
//     Player(){}
//
//     quint64 id;
//     quint64 clientId;
//     QString name;
//
// };


class RoomManager:public QObject
{
    Q_OBJECT
public:
    RoomManager(QObject* parent=nullptr);

    void handleLobbySync(quint64 clientId,const LobbyMessage::LobbySyncRequest& message);
    void joinRoom(QString name, quint64 clientId);
    void leaveRoom(QString name);
    void startRoom();
    void endRoom();

private:
    QHash<QString,quint64> m_players;//名称 客户端id
signals:
    void sendTcpMessage(quint64 clientId,const QByteArray &message);
    void sendUdpMessage(quint64 clientId,const QByteArray &message);
};


#endif //SERVER_ROOMMANAGER_H