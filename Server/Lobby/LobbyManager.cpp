//
// Created by StarFlame on 2026/3/29.
//
#define FILE_PREFIX "LobbyManager:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Debug//局部日志等级

#include "LobbyManager.h"
#include "../LoggerStream.h"
#include "../protobuf/output/SyncMessage.pb.h"

LobbyManager::LobbyManager(QObject* parent)
    :QObject(parent),m_playerManager(this),m_roomManager(this)
{
}

void LobbyManager::handleLobbySync(quint64 clientId, const LobbyMessage::LobbySyncRequest& message)
{

    using namespace LobbyMessage;
    switch (message.content_case())
    {
        case LobbySyncRequest::kJoinRoom:
            Log_Debug()<<"[handeleLobbySync]kJoinRoom";
            m_roomManager.joinRoom(QString::fromStdString(message.joinroom().name()),clientId);
            break;
        case LobbySyncRequest::kLeaveRoom:
            Log_Debug()<<"[handeleLobbySync]kLeaveRoom";
            m_roomManager.leaveRoom(QString::fromStdString(message.leaveroom().name()));
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


