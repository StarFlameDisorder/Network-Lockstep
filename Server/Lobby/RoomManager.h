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
    quint64 lastFrameId;//最新收到的帧id
    std::queue<GameMessage::PlayerSync> receiveMessages;//收到的帧同步消息
    bool online;

    QHash<quint64,GameMessage::PlayerSync> frames;//历史帧记录 帧序号 帧
    std::queue<quint64> currentFrameIds;//TODO:改为Qqueue
    quint64 preSnapshotId=0;//上次快照中的帧id
};

class RoomManager:public QObject
{
    Q_OBJECT
public:
    RoomManager(QObject* parent=nullptr);

    void handleLobbySync(quint64 clientId,const LobbyMessage::LobbySyncRequest& message);
    void joinRoom(QString name, quint64 clientId);
    void leaveRoom(QString name,quint64 clientId);
    void startRoom(QString name);
    void endRoom();

    void receiveGameSync(quint64 clientId,const GameMessage::GameSyncMessage& message);
    void receiveSnapshot(quint64 clientId,const GameMessage::GameSnapshotMessage &message);
    void receiveHeartBeat(quint64 clientId,const GameMessage::HeartBeat& message);

    void receiveClientDisconnection(quint64 clientId);//接收tcp断线消息

private:
    void broadcastGameSync();
    quint64 containPlayer(QString name);

    QHash<quint64,quint64> m_playerId;//客户端id 玩家id
    QHash<quint64,Player> m_players;//玩家id 玩家信息
    QTimer m_timer;//定时发送
    quint64 m_nextPlayerId=1;//从1开始
    quint64 m_sendIndex=0;
    bool isRunning=false;

    //TODO:帧的处理未完成 分发、删除 帧应该是已经发送过的 现在把未发送的也塞里面了
    GameMessage::GameSnapshot m_gameSnapshot;//最新快照

signals:
    void sendTcpMessage(quint64 clientId,const QByteArray &message);
    void sendUdpMessage(quint64 clientId,const QByteArray &message);

    void removeClient(quint64 clientId);//向下传播
};


#endif //SERVER_ROOMMANAGER_H