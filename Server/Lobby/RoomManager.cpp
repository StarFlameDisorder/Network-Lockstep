/*
* Created by StarFlame on 2026/3/28.
 * 房间数据管理及同步
 */

#define FILE_PREFIX "RoomManager:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Info//局部日志等级
#define GameFrameRate 30

#include "RoomManager.h"
#include "../LoggerStream.h"

//TODO:断线重连
RoomManager::RoomManager(QObject* parent):QObject(parent),m_timer(this)
{
    connect(&m_timer,&QTimer::timeout,this,&RoomManager::broadcastGameSync);
}

void RoomManager::handleLobbySync(quint64 clientId, const LobbyMessage::LobbySyncRequest& message)
{

    using namespace LobbyMessage;
    switch (message.content_case())
    {
        case LobbySyncRequest::kJoinRoom:
            Log_Debug()<<"[handeleLobbySync]kJoinRoom";
            joinRoom(QString::fromStdString(message.joinroom().name()),clientId);
            break;
        case LobbySyncRequest::kLeaveRoom:
            Log_Debug()<<"[handeleLobbySync]kLeaveRoom";
            leaveRoom(QString::fromStdString(message.leaveroom().name()),clientId);
            break;
        case LobbySyncRequest::kStartRoom:
            Log_Debug()<<"[handeleLobbySync]kStartRoom";
            startRoom(QString::fromStdString(message.startroom().name()));
            break;
        case LobbySyncRequest::kEndRoom:
            Log_Debug()<<"[handeleLobbySync]kEndRoom";
            break;
        default:
            Log_Error()<<"[handeleLobbySync]未知类型:"<<message.content_case();
            break;
    }
}

void RoomManager::joinRoom(QString name, quint64 clientId)
{
    quint64 playerId=containPlayer(name);
    if (playerId!=0)Log_Warning()<<"出现同名：断线重连/玩家重名";
    else
    {
        playerId=m_nextPlayerId;
        m_nextPlayerId++;
    }

    Player &player= m_players[playerId];
    player.id=playerId;
    player.clientId=clientId;
    player.name=name;
    player.activeTime=QDateTime::currentMSecsSinceEpoch();
    player.receiveMessages={};//清空
    player.online=true;

    m_playerId[clientId]=playerId;

    using namespace SyncMessage;
    using namespace LobbyMessage;

    ServerMessage sendMessage;
    auto *lobbyMes=sendMessage.mutable_lobbysync();
    auto *playerJoin=lobbyMes->mutable_joinroom();
    for (auto i:m_players)
    {
        playerJoin->add_players(i.name.toStdString());
    }

    QByteArray data;
    data.resize(sendMessage.ByteSizeLong());
    bool success = sendMessage.SerializeToArray(data.data(), data.size());
    if (!success) {
        Log_Error()<<"[joinRoom]无法生成ServerMessage.";
        return;
    }

    for (auto &i:m_players)
    {
        emit sendTcpMessage(i.id,data);
    }

    Log_Info()<<"[joinRoom]玩家加入:"<<name<<"客户端Id:"<<clientId<<"玩家id:"<<playerId<<"总数："<<m_players.size();
    //if (m_players.size()>0)startRoom();
}

void RoomManager::leaveRoom(QString name,quint64 clientId)
{
    qint64 playerId=m_playerId[clientId];
    if (m_players[playerId].name!=name)
    {
        Log_Error()<<"[leaveRoom]名称不一致"<<clientId<<name<<"内部名称"<<m_players[playerId].name;
    }

    m_players.remove(playerId);
    m_playerId.remove(clientId);

    Log_Info()<<"[leaveRoom]玩家离开:"<<name<<"客户端Id:"<<clientId;
    if (m_players.size()==0)endRoom();
}

void RoomManager::startRoom(QString name)
{
    Log_Info()<<"[startRoom]开始"<<name;
    m_timer.start(((float)1000)/GameFrameRate);

    using namespace SyncMessage;
    using namespace LobbyMessage;
    ServerMessage sendMessage;
    auto *lobbyMes=sendMessage.mutable_lobbysync();
    auto *startMes=lobbyMes->mutable_startroom();
    startMes->set_name(name.toStdString());

    QByteArray data;
    data.resize(sendMessage.ByteSizeLong());
    bool success = sendMessage.SerializeToArray(data.data(), data.size());
    if (!success) {
        Log_Error()<<"[startRoom]无法生成ServerMessage.";
        return;
    }

    for (auto &i:m_players)
    {
        emit sendTcpMessage(i.id,data);
    }
}

void RoomManager::endRoom()
{
    Log_Info()<<"[endRoom]结束";
}

void RoomManager::receiveGameSync(quint64 clientId,const GameMessage::GameSyncMessage& message)
{
    using namespace GameMessage;
    quint64 playerId=m_playerId[clientId];

    if (message.players_size()>0)
    {
        queue<PlayerSync> &queue=m_players[playerId].receiveMessages;
        PlayerSync p;
        p.CopyFrom(message.players(0));
        queue.push(p);
    }
}

void RoomManager::receiveHeartBeat(quint64 clientId, const GameMessage::HeartBeat& message)
{
    Log_Debug()<<"[receiveHeartBeat]收到来自"<<clientId<<" "<<message.name();
    m_players[m_playerId[clientId]].activeTime=QDateTime::currentMSecsSinceEpoch();
    m_players[m_playerId[clientId]].online=true;

}

//断线判断逻辑：tcp断开连接/心跳超时
void RoomManager::receiveClientDisconnection(quint64 clientId)
{
    auto &p=m_players[m_playerId[clientId]];
    if (p.online==true)
    {
        p.online=false;
        Log_Info()<<p.name<<"tcp断开连接，玩家断线";
    }
}

void RoomManager::broadcastGameSync()
{
    for (auto &i:m_players)
    {
        if (i.online==true)
        {
            if (QDateTime::currentMSecsSinceEpoch()-i.activeTime>4000)
            {
                i.online=false;
                Log_Info()<<i.name<<"心跳超时，玩家断线";
            }
        }
    }

    QString pack=QString::number(m_sendIndex)+":";
    m_sendIndex++;

    using namespace GameMessage;
    using namespace SyncMessage;

    ServerMessage sendMessage;
    GameSyncMessage* newGameSyncMessage= sendMessage.mutable_gamesyncmessage();
    for (auto &p:m_players)
    {
        queue<PlayerSync> &queue=p.receiveMessages;
        while (!queue.empty())
        {
            PlayerSync sync=queue.front();

            pack.append(" "+sync.name()+"-");
            pack.append(QString::number(sync.frameid()));

            newGameSyncMessage->add_players()->CopyFrom(sync);
            queue.pop();
        }
    }

    if (pack.size()>0)Log_Debug()<<pack;

    QByteArray data;
    data.resize(sendMessage.ByteSizeLong());
    bool success = sendMessage.SerializeToArray(data.data(), data.size());
    if (!success) {
        Log_Error()<<"Failed to serialize ServerMessage.";
        return;
    }
    for (auto &i:m_players)
    {
        if (i.online)emit sendUdpMessage(i.clientId,data);
    }
}

quint64 RoomManager::containPlayer(QString name)
{
    for (auto &i:m_players)
    {
        if (i.name==name)return i.id;
    }
    return 0;
}

