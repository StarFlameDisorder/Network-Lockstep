//
// Created by StarFlame on 2026/3/28.
//

#define FILE_PREFIX "RoomManager:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Debug//局部日志等级

#include "RoomManager.h"
#include "../LoggerStream.h"

RoomManager::RoomManager(QObject* parent):QObject(parent)
{
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

    using namespace SyncMessage;
    using namespace LobbyMessage;

    ServerMessage sendMessage;
    auto *lobbyMes=sendMessage.mutable_lobbysync();
    auto *playerJoin=lobbyMes->mutable_joinroom();
    playerJoin->set_name(name.toStdString());

    QByteArray data;
    data.resize(sendMessage.ByteSizeLong());
    bool success = sendMessage.SerializeToArray(data.data(), data.size());
    if (!success) {
        Log_Error()<<"[joinRoom]无法生成ServerMessage.";
        return;
    }
    emit sendTcpMessage(clientId,data);

    Log_Info()<<"[joinRoom]玩家加入:"<<name<<"客户端Id:"<<clientId;
}

void RoomManager::leaveRoom(QString name)
{
    quint64 clientId=m_players.value(name);
    m_players.remove(name);

    Log_Info()<<"[leaveRoom]玩家离开:"<<name<<"客户端Id:"<<clientId;
}

void RoomManager::startRoom()
{
    Log_Info()<<"[startRoom]开始";
}

void RoomManager::endRoom()
{
    Log_Info()<<"[endRoom]结束";
}

