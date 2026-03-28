//
// Created by StarFlame on 2026/2/28.
//

#define FILE_PREFIX "Dispatcher:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Info//局部日志等级

#include "NetworkDispatcher.h"
#include "../LoggerStream.h"

using namespace SyncMessage;
using namespace ConnectMessage;

NetworkDispatcher::NetworkDispatcher(QObject *parent):QObject(parent),m_tcpServer(this),m_udpServer(this)
{
    startTime=QDateTime::currentMSecsSinceEpoch();
    connect(&m_tcpServer,&TcpServer::addNewClient,this,&NetworkDispatcher::addClient);//客户端id分配

    connect(&m_tcpServer,&TcpServer::receiveMessage,this,&NetworkDispatcher::handleTcpMessage);
    connect(&m_udpServer,&UdpServer::receiveMessage,this,&NetworkDispatcher::handleUdpMessage);

}

NetworkDispatcher::~NetworkDispatcher()
{
    quint64 endTime=QDateTime::currentMSecsSinceEpoch();
    //qDebug()<<"服务器运行时处理包:"<<messageNum;
}

void NetworkDispatcher::sendTcpMessage(QTcpSocket *socket,const QByteArray &message)
{
    m_tcpServer.sendMessage(socket,message);
}


void NetworkDispatcher::sendUdpMessage(const QHostAddress& address, quint16 port, const QByteArray& message)
{
    m_udpServer.sendMessage(address,port,message);
}

void NetworkDispatcher::handleTcpMessage(QTcpSocket* socket, const QByteArray& message)
{
    messageNum++;
    ClientMessage clientMessage;
    clientMessage.ParseFromArray(message,message.size());
    quint64 clientId=clientMessage.clientid();
    checkClient(clientId,socket);
    switch (clientMessage.content_case())
    {
        case ClientMessage::kCommonMessage:
            Log_Info()<<"[handleTcpMessage][Tcp:"<<m_tcpServer.getTcpSocketInfo(socket)<<"-"<<clientId<<"]"<<clientMessage.commonmessage();
            break;
        case ClientMessage::kConnectMessage:
            handleTcpConnection(socket,clientMessage.connectmessage());
            break;
        case ClientMessage::kGameSyncMessage:
            handleGameSync(clientId,clientMessage.gamesyncmessage());
            break;
        default:
            Log_Error()<<"[handleTcpMessage]未知类型:"<<clientMessage.content_case();
            break;
    }
}

void NetworkDispatcher::handleTcpConnection(QTcpSocket* socket, const ClientConnectMessage &message)
{
    switch(message.content_case())
    {
        case ClientConnectMessage::kHandShakeMessage://tcp客户端不会发送HandShakeMessage
            Log_Info()<<message.handshakemessage().content();
            break;
        default:
            Log_Error()<<"[handleTcpConnection]未知类型:"<<message.content_case();
            break;
    }
}

void NetworkDispatcher::handleUdpMessage(const QHostAddress& address, const quint16& port,const QByteArray& message)
{
    messageNum++;
    ClientMessage clientMessage;
    clientMessage.ParseFromArray(message,message.size());
    quint64 clientId=clientMessage.clientid();
    checkClient(clientId,address,port);
    switch (clientMessage.content_case())
    {
    case ClientMessage::kCommonMessage:
        Log_Info()<<"[handleUdpMessage][Udp:"<<m_udpServer.getPeerAddressInfo(address,port)<<"-"<<clientId<<"]"<<clientMessage.commonmessage();
        break;
    case ClientMessage::kConnectMessage:
        handleUdpConnection(address,port,clientMessage.connectmessage());
        break;
    case ClientMessage::kGameSyncMessage:
        handleGameSync(clientId,clientMessage.gamesyncmessage());
        break;
    default:
        Log_Error()<<"[handleUdpMessage]未知类型:"<<clientMessage.content_case();
        break;
    }
}

void NetworkDispatcher::handleUdpConnection(const QHostAddress &address, const quint16 &port, const ClientConnectMessage &message)
{
    switch(message.content_case())
    {
    case ClientConnectMessage::kHandShakeMessage:
        Log_Info()<<message.handshakemessage().content()<<": "<<m_udpServer.getPeerAddressInfo(address,port)<<"绑定id-"<<message.handshakemessage().clientid();
        bindClient(message.handshakemessage().clientid(),UdpEndPoint(address,port));
        break;
    default:
        Log_Error()<<"[handleUdpConnection]未知类型:"<<message.content_case();
        break;
    }
}

void NetworkDispatcher::handleGameSync(quint64 clientId, const GameSyncMessage& message)
{
    using namespace GameMessage;
    broadcastGameSync(message,clientId);
}

void NetworkDispatcher::checkClient(qint64 clientId, QTcpSocket* socket)
{
    if (m_clientsMap.contains(clientId))
    {
        bindClient(clientId,socket);
    }
    else
    {
        Log_Error()<<"[checkClient]未知客户端id:[Tcp:"<<m_tcpServer.getTcpSocketInfo(socket)<<"-"<<clientId<<"]";
    }
}

void NetworkDispatcher::checkClient(qint64 clientId, const QHostAddress& address, quint16 port)
{
    if (m_clientsMap.contains(clientId))
    {
        bindClient(clientId,UdpEndPoint(address,port));
    }
    else
    {
        Log_Error()<<"[checkClient]未知客户端id:[Udp:"<<m_udpServer.getPeerAddressInfo(address,port)<<"-"<<clientId<<"]";
    }
}

Client NetworkDispatcher::findClient(qint64 clientId)
{
    return m_clientsMap[clientId];
}

void NetworkDispatcher::addClient(QTcpSocket* socket)
{
    quint64 clientId;
    // if (!m_tcpClientsMap.contains(TcpEndPoint(socket)))
    // {
    //     clientId=nextClientId;
    //     Client client;
    //     client.clientId=clientId;
    //     nextClientId++;
    //     m_clientsMap.insert(clientId,client);
    //     m_tcpClientsMap.insert(TcpEndPoint(socket),clientId);
    // }else clientId=m_tcpClientsMap[TcpEndPoint(socket)];
    clientId=nextClientId;
    Client client;
    client.clientId=clientId;
    nextClientId++;
    m_clientsMap.insert(clientId,client);

    using namespace ConnectMessage;

    using namespace SyncMessage;
    ServerMessage message;
    auto *connectMessage= message.mutable_connectmessage();
    auto *response=connectMessage->mutable_handshakemessage();
    response->set_content("Tcp-这里是服务器,建立连接");
    response->set_clientid(clientId);
    QByteArray data;
    data.resize(message.ByteSizeLong());
    message.SerializeToArray(data.data(),data.size());

    sendTcpMessage(socket,data);
    Log_Info()<<"分配id"<<clientId<<":"<<m_tcpServer.getTcpSocketInfo(socket);
}

void NetworkDispatcher::bindClient(const quint64 clientId, QTcpSocket* tcpSocket)
{
    if(m_clientsMap.contains(clientId)&&m_tcpSocketClientsMap[tcpSocket]!=clientId)
    {
        m_clientsMap[clientId].socket=tcpSocket;//修改数据本体
        m_tcpSocketClientsMap[tcpSocket]=clientId;
        Log_Debug()<<"[bindClient]绑定客户端id:[Tcp:"<<m_tcpServer.getTcpSocketInfo(tcpSocket)<<"-"<<clientId<<"]";
    }
}

void NetworkDispatcher::bindClient(const quint64 clientId, const UdpEndPoint& udpEndPoint)
{
    if(m_clientsMap.contains(clientId)&&m_udpClientsMap[udpEndPoint]!=clientId)
    {
        m_clientsMap[clientId].udpEndPoint=udpEndPoint;
        m_udpClientsMap[udpEndPoint]=clientId;
        Log_Debug()<<"[bindClient]绑定客户端id:[Udp:"<<m_udpServer.getPeerAddressInfo(udpEndPoint)<<"-"<<clientId<<"]";
    }
}

void NetworkDispatcher::broadcastGameSync(const GameSyncMessage& message,quint64 clientId)
{
    Log_Debug()<<"broadcastGameSync!";

    // 创建一个新的GameSyncMessage并深复制PlayerSync内容
    ServerMessage sendMessage;

    // 深复制
    if (message.players_size()>0)
    {
        GameSyncMessage* newGameSyncMessage= sendMessage.mutable_gamesyncmessage();
        newGameSyncMessage->set_frameid(message.frameid());
        newGameSyncMessage->set_roomid(message.roomid());
        newGameSyncMessage->set_time(message.time());
        for (auto &player:message.players())
        {
            *newGameSyncMessage->add_players() = player;
        }
    }

    QByteArray data;
    data.resize(sendMessage.ByteSizeLong());
    bool success = sendMessage.SerializeToArray(data.data(), data.size());
    if (!success) {
        Log_Error()<<"Failed to serialize ServerMessage.";
        return;
    }
    UdpEndPoint uep=m_clientsMap[clientId].udpEndPoint;
    sendUdpMessage(uep.address,uep.port, data);
    // for (const auto& i : m_clientsMap) {
    //     sendUdpMessage(i.udpEndPoint.address,i.udpEndPoint.port, data);
    // }
}

