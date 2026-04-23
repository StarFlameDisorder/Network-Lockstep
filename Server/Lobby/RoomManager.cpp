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
    bool samePlayer=false;

    if (playerId!=0)
    {
        Log_Warning()<<"出现同名：断线重连/玩家重名";
        samePlayer=true;
    }
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

    {
        ServerMessage sendMessage;
        auto *lobbyMes=sendMessage.mutable_lobbysync();
        auto *playerJoin=lobbyMes->mutable_joinroom();
        for (auto i:m_players)
        {
            playerJoin->add_players(i.name.toStdString());
        }

        if (m_players.contains(1))playerJoin->set_owner(m_players[1].name.toStdString());

        QByteArray data;
        data.resize(sendMessage.ByteSizeLong());
        bool success = sendMessage.SerializeToArray(data.data(), data.size());
        if (!success) {
            Log_Error()<<"[joinRoom]无法生成ServerMessage.";
            return;
        }

        for (auto &i:m_players)
        {
            emit sendTcpMessage(i.clientId,data);
        }
    }

    Log_Info()<<"[joinRoom]玩家加入:"<<name<<"客户端Id:"<<clientId<<"玩家id:"<<playerId<<"总数："<<m_players.size();

    //断线重连
    if (samePlayer&&isRunning)
    {
        {
            ServerMessage sendMessage;
            auto *snapMess=sendMessage.mutable_gamesnapshotmessage();
            auto *snapshot=snapMess->mutable_snapshot();
            snapshot->CopyFrom(m_gameSnapshot);
            snapshot->set_lastframeid(m_players[playerId].lastFrameId);

            QByteArray data;
            data.resize(sendMessage.ByteSizeLong());
            bool success = sendMessage.SerializeToArray(data.data(), data.size());
            if (!success) {
                Log_Error()<<"Failed to serialize ServerMessage.";
                return;
            }
            emit sendUdpMessage(clientId,data);
            Log_Info()<<"[断线重连]补发快照 包含玩家数:"<<snapshot->playersss_size();
        }

        {
            using namespace GameMessage;

            //TODO:UDP重构 支持分包
            QString log("补发帧:");
            const int MAX_FRAMES_PER_PACKET = 10;  // 每包最多10帧
            QVector<ServerMessage> packets;
            int frameCount = 0;

            ServerMessage currentPacket;
            auto *snapshotMessage=currentPacket.mutable_gamesnapshotmessage();
            auto *frames=snapshotMessage->mutable_frames();
            frames->set_frameid(m_gameSnapshot.frameid());

            for (auto &p : m_players) {
                for (quint64 i = p.preSnapshotId + 1; p.frames.contains(i); i++) {
                    frames->add_players()->CopyFrom(p.frames[i]);
                    log.append(" "+p.name+"-"+QString::number(i));
                    frameCount++;
                    if (frameCount >= MAX_FRAMES_PER_PACKET) {
                        log.append(" "+p.name+"-总共"+QString::number(frames->players_size()));
                        packets.append(currentPacket);
                        frames->clear_players();
                        frameCount = 0;
                    }
                }
            }
            if (frameCount > 0) packets.append(currentPacket);

            // 发送每个分包
            for (auto &pkt : packets) {
                QByteArray data;
                data.resize(pkt.ByteSizeLong());
                pkt.SerializeToArray(data.data(), data.size());
                emit sendUdpMessage(clientId, data);
            }
            Log_Info()<<log<<"总包数："<<packets.size();
        }

    }

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
    isRunning=true;
    Log_Info()<<"[startRoom]开始"<<name;
    m_timer.start(((float)1000)/GameFrameRate);

    using namespace SyncMessage;
    using namespace LobbyMessage;
    ServerMessage sendMessage;
    auto *lobbyMes=sendMessage.mutable_lobbysync();
    auto *startMes=lobbyMes->mutable_startroom();

    if (m_players.contains(1))startMes->set_name(m_players[1].name.toStdString());
    else startMes->set_name(name.toStdString());

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
    isRunning=false;
}

void RoomManager::receiveGameSync(quint64 clientId,const GameMessage::GameSyncMessage& message)
{
    using namespace GameMessage;
    quint64 playerId=m_playerId[clientId];

    if (message.players_size()>0)
    {
        queue<PlayerSync> &queue=m_players[playerId].receiveMessages;
        PlayerSync p;
        p.CopyFrom(message.players(0));//因为只有一个玩家的操作输入
        queue.push(p);
        m_players[playerId].lastFrameId=p.frameid();
        PlayerSync snapFrame;
        snapFrame.CopyFrom(message.players(0));
        m_players[playerId].frames.insert(snapFrame.frameid(),snapFrame);
    }
}

void RoomManager::receiveSnapshot(quint64 clientId, const GameMessage::GameSnapshotMessage& message)
{
    using namespace GameMessage;
    if (message.content_case()!=GameSnapshotMessage::kSnapshot)Log_Error()<<"[receiveSnapshot]接收消息 clientId:"<<clientId<<"快照错误类型";

    const GameSnapshot &snapshot=message.snapshot();

    Log_Info()<<"[receiveSnapshot]接收消息 clientId:"<<clientId<<"玩家人数"<<snapshot.playersss_size()<<"结束帧"<<snapshot.frameid();

    m_gameSnapshot=message.snapshot();
    for (auto &s:snapshot.playersss())
    {
        quint64 playerId=containPlayer(QString::fromStdString(s.name()));
        if (playerId!=0)
        {
            Player &p=m_players[playerId];
            quint64 oldFrameId=p.preSnapshotId;
            quint64 newFrameId=s.frameid();

            p.preSnapshotId=s.frameid();

            QString log1("移除");
            while (!p.currentFrameIds.empty())
            {
                quint64 frameId=p.currentFrameIds.front();
                if (!p.frames.contains(frameId)||frameId>newFrameId)break;
                p.frames.remove(p.currentFrameIds.front());
                p.currentFrameIds.pop();
                log1+=" "+QString::number(frameId);
            }
            Log_Info()<<"玩家"<<p.name<<"剩余"<<p.frames.size()<<"帧"<<log1;
            if (!p.currentFrameIds.empty())Log_Info()<<"剩余帧数"<<p.currentFrameIds.size()<<"最旧id"<<p.currentFrameIds.front();
            QString log("剩余帧:");
            for (auto &i:p.frames.keys())
            {
                log+=" "+QString::number(i);
            }
            Log_Info()<<"玩家"<<p.name<<log;

        }else continue;
    }

}

void RoomManager::receiveHeartBeat(quint64 clientId, const GameMessage::HeartBeat& message)
{
    Log_Debug()<<"[receiveHeartBeat]收到来自"<<clientId<<" "<<message.name();
    m_players[m_playerId[clientId]].activeTime=QDateTime::currentMSecsSinceEpoch();
    m_players[m_playerId[clientId]].online=true;

}

//断线判断逻辑：tcp断开连接/心跳超时
void RoomManager::receiveClientDisconnection(quint64 clientId)//TODO:玩家断线时，清除多余的记录的帧？
{
    auto &p=m_players[m_playerId[clientId]];
    if (p.online==true)
    {
        p.online=false;
        Log_Info()<<p.name<<"tcp断开连接，玩家断线";
        emit removeClient(clientId);
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
                emit removeClient(i.clientId);
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

            p.frames.insert(sync.frameid(),sync);//已发送的帧缓存 便于断线重连
            p.currentFrameIds.push(sync.frameid());
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

