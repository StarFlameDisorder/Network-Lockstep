//
// Created by StarFlame on 2026/3/28.
//
#define FILE_PREFIX "PlayerManager:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Info//局部日志等级

#include "PlayerManager.h"
#include "../LoggerStream.h"

PlayerManager::PlayerManager(QObject* parent):QObject(parent)
{
}

quint64 PlayerManager::addPlayer(QString name, quint64 clientId)
{
    quint64 playerId=findPlayer(name);
    if (playerId==0)
    {
        playerId=m_nextPlayerId;
        m_playersNameMap[name]=playerId;
        m_clientIdMap[clientId]=playerId;
        Player p=Player(playerId,name);
        p.clientId=clientId;
        m_playerMap.insert(playerId,p);
        m_nextPlayerId++;
        Log_Info()<<"[addPlayer]添加玩家:"<<name<<",playerId"<<playerId;
    }else
    {
        Log_Warning()<<"[addPlayer]发现"<<name<<"重复请求playerId"<<playerId;
    }
    return playerId;
}

Player &PlayerManager::findPlayerById(quint64 playerId)
{
    return m_playerMap[playerId];
}

quint64 PlayerManager::findPlayer(QString name)
{
    return m_playersNameMap.value(name);
}

quint64 PlayerManager::findPlayer(quint64 clientId)
{
    return m_clientIdMap.value(clientId);
}

void PlayerManager::bindClientId(quint64 playerId, quint64 clientId)
{
    if (m_playerMap.contains(playerId))
    {
        m_clientIdMap.remove(m_playerMap[playerId].clientId);
        m_clientIdMap[clientId]=playerId;
        m_playerMap[playerId].clientId=clientId;
    }else Log_Error()<<"错误：无此玩家id"<<playerId;
}
