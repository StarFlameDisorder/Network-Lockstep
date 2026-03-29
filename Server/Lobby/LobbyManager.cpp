//
// Created by StarFlame on 2026/3/29.
//
#define FILE_PREFIX "LobbyManager:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Debug//局部日志等级

#include "LobbyManager.h"
#include "../LoggerStream.h"

LobbyManager::LobbyManager(QObject* parent)
    :QObject(parent),m_playerManager(this),m_roomManager(this)
{
}

void LobbyManager::handleLobbySync(quint64 clientId, const LobbyMessage::LobbySyncRequest& message)
{

    using namespace LobbyMessage;
    switch (message.content_case())
    {
        case LobbySyncRequest::kPlayerLogin:
            Log_Debug()<<"[handeleLobbySync]kPlayerLogin";
            break;
        case LobbySyncRequest::kPlayerJoin:
            Log_Debug()<<"[handeleLobbySync]kPlayerJoin";
            break;
        case LobbySyncRequest::kPlayerPlayRoom:
            Log_Debug()<<"[handeleLobbySync]kPlayePlayRoom";
            break;
        default:
            Log_Error()<<"[handeleLobbySync]未知类型:"<<message.content_case();
            break;
    }
}
