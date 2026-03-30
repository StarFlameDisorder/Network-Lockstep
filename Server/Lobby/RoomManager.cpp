//
// Created by StarFlame on 2026/3/28.
//

#define FILE_PREFIX "RoomManager:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Debug//局部日志等级


#include "RoomManager.h"
#include "../LoggerStream.h"

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
            leaveRoom(QString::fromStdString(message.leaveroom().name()));
            break;
        case LobbySyncRequest::kStartRoom:
            Log_Debug()<<"[handeleLobbySync]kStartRoom";
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
    if (m_players.contains(name))Log_Warning()<<"出现同名：断线重连/玩家重名";
    m_players[name]=clientId;
    m_playersName[clientId]=name;

    using namespace SyncMessage;
    using namespace LobbyMessage;

    ServerMessage sendMessage;
    auto *lobbyMes=sendMessage.mutable_lobbysync();
    auto *playerJoin=lobbyMes->mutable_joinroom();
    //playerJoin->set_name(name.toStdString());
    for (auto i:m_players.keys())
    {
        playerJoin->add_players(i.toStdString());
    }

    QByteArray data;
    data.resize(sendMessage.ByteSizeLong());
    bool success = sendMessage.SerializeToArray(data.data(), data.size());
    if (!success) {
        Log_Error()<<"[joinRoom]无法生成ServerMessage.";
        return;
    }

    for (auto i:m_players)
    {
        emit sendTcpMessage(i,data);
    }

    Log_Info()<<"[joinRoom]玩家加入:"<<name<<"客户端Id:"<<clientId;
    if (m_players.size()>0)startRoom();
}

void RoomManager::leaveRoom(QString name)
{
    quint64 clientId=m_players.value(name);
    m_players.remove(name);
    m_playersName.remove(clientId);

    Log_Info()<<"[leaveRoom]玩家离开:"<<name<<"客户端Id:"<<clientId;
    if (m_players.size()==0)endRoom();
}

void RoomManager::startRoom()
{
    Log_Info()<<"[startRoom]开始";
    m_timer.start(((float)1000)/15);
}

void RoomManager::endRoom()
{
    Log_Info()<<"[endRoom]结束";
}

void RoomManager::receiveGameSync(quint64 clientId,const GameMessage::GameSyncMessage& message)
{
    using namespace GameMessage;
    if (!m_messages.contains(clientId))
    {
        m_messages.insert(clientId,queue<PlayerSync>());
    }

    if (message.players_size()>0)
    {
        queue<PlayerSync> &queue=m_messages[clientId];
        PlayerSync p;
        p.CopyFrom(message.players(0));
        queue.push(p);
    }
}

void RoomManager::broadcastGameSync()
{
    using namespace GameMessage;
    using namespace SyncMessage;

    ServerMessage sendMessage;
    GameSyncMessage* newGameSyncMessage= sendMessage.mutable_gamesyncmessage();
    for (auto &i:m_messages)
    {
        if (!i.empty())
        {
            PlayerSync sync=i.front();
            newGameSyncMessage->add_players()->CopyFrom(sync);
            i.pop();
        }
    }
    //QString out;
    for (auto &i:newGameSyncMessage->players())
    {
        UnityMath::Vector3D v3= i.inputmove();
        //out+="z"+QString::number(v3.x())+"y"+QString::number(v3.y())+"z"+QString::number(v3.z());
    }

    //Log_Debug() <<"玩家数"<<newGameSyncMessage->players_size()<<out;

    QByteArray data;
    data.resize(sendMessage.ByteSizeLong());
    bool success = sendMessage.SerializeToArray(data.data(), data.size());
    if (!success) {
        Log_Error()<<"Failed to serialize ServerMessage.";
        return;
    }
    for (auto i:m_players)
    {
        emit sendUdpMessage(i,data);
    }
}

