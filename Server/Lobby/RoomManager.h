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
#include <QDateTime>

#include "../protobuf/output/SyncMessage.pb.h"

using std::queue;

struct Player{
    Player(){}

    quint64 id;//玩家id
    quint64 clientId;//客户端id
    QString name;//名称
    quint64 activeTime;//上次心跳时间
    std::queue<GameMessage::PlayerSync> receiveMessages;//收到的帧同步消息
};

class RoomManager:public QObject
{
    Q_OBJECT
public:
    RoomManager(QObject* parent=nullptr);

    void handleLobbySync(quint64 clientId,const LobbyMessage::LobbySyncRequest& message);
    void joinRoom(QString name, quint64 clientId);
    void leaveRoom(QString name,quint64 clientId);
    void startRoom();
    void endRoom();
    void receiveGameSync(quint64 clientId,const GameMessage::GameSyncMessage& message);
    void receiveHeartBeat(quint64 clientId,const GameMessage::HeartBeat& message);

private:
    void broadcastGameSync();
    quint64 containPlayer(QString name);

    QHash<quint64,quint64> m_playerId;//客户端id 玩家id
    QHash<quint64,Player> m_players;//玩家id 玩家信息
    QTimer m_timer;//定时发送
    quint64 m_nextPlayerId=1;//从1开始

signals:
    void sendTcpMessage(quint64 clientId,const QByteArray &message);
    void sendUdpMessage(quint64 clientId,const QByteArray &message);
};


#endif //SERVER_ROOMMANAGER_H