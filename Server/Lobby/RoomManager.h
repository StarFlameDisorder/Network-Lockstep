/*
 * Created by StarFlame on 2026/3/28.
 * 房间数据管理及同步
 */

#ifndef SERVER_ROOMMANAGER_H
#define SERVER_ROOMMANAGER_H


#include <QObject>
#include <QTimer>
#include <QHash>
#include <queue>

#include "../protobuf/output/SyncMessage.pb.h"

using std::queue;

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
    void receiveGameSync(quint64 clientId,const GameMessage::GameSyncMessage& message);

private:
    void broadcastGameSync();

    QHash<QString,quint64> m_players;//名称 客户端id
    QHash<quint64,QString> m_playersName;
    QHash<quint64,std::queue<GameMessage::PlayerSync>> m_messages;
    QTimer m_timer;//定时发送

signals:
    void sendTcpMessage(quint64 clientId,const QByteArray &message);
    void sendUdpMessage(quint64 clientId,const QByteArray &message);
};


#endif //SERVER_ROOMMANAGER_H