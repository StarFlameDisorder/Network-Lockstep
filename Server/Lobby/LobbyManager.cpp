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
        case LobbySyncRequest::kPlayerLogin:
            Log_Debug()<<"[handeleLobbySync]kPlayerLogin";
            addPlayer(clientId,message.playerlogin());
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

void LobbyManager::addPlayer(quint64 clientId, const LobbyMessage::PlayerLoginRequest& message)
{
    using namespace SyncMessage;
    using namespace LobbyMessage;

    quint64 playerId = m_playerManager.addPlayer(QString::fromStdString(message.name()),clientId);
    ServerMessage sendMessage;
    auto *lobbyMes=sendMessage.mutable_lobbysync();
    auto *playerLogin=lobbyMes->mutable_playerlogin();
    playerLogin->set_playerid(playerId);

    QByteArray data;
    data.resize(sendMessage.ByteSizeLong());
    bool success = sendMessage.SerializeToArray(data.data(), data.size());
    if (!success) {
        Log_Error()<<"Failed to serialize ServerMessage.";
        return;
    }
    emit sendTcpMessage(clientId,data);
}
