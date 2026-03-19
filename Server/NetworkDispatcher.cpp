//
// Created by StarFlame on 2026/2/28.
//

#define FILE_PREFIX "Dispatcher:"//日志前缀
#define LOCAL_LOG_LEVEL LogLevel::Debug//局部日志等级

#include "NetworkDispatcher.h"
#include "LoggerStream.h"

using namespace SyncMessage;
using namespace ConnectMessage;


NetworkDispatcher::NetworkDispatcher():m_tcpServer(this),m_udpServer(this)
{
}

void NetworkDispatcher::sendTcpMessage(QTcpSocket *socket,const QByteArray &message)
{
    m_tcpServer.sendMessageBySocket(socket,message);
}


void NetworkDispatcher::sendUdpMessage(const QHostAddress& address, quint16 port, const QByteArray& message)
{
    m_udpServer.sendMessage(address,port,message);
}

void NetworkDispatcher::handleTcpMessage(QTcpSocket* socket, const QByteArray& message)
{
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
        default:
            Log_Error()<<"[handleTcpMessage]未知类型:"<<clientMessage.content_case();
            break;
    }
}

void NetworkDispatcher::handleTcpConnection(QTcpSocket* socket, const ClientConnectMessage &message)
{
    switch(message.content_case())
    {
        case ClientConnectMessage::kHandShakeMessage:
            Log_Info()<<message.handshakemessage().content();
            break;
        default:
            Log_Error()<<"[handleTcpConnection]未知类型:"<<message.content_case();
            break;
    }
}

void NetworkDispatcher::handleUdpMessage(const QHostAddress &address, quint16 port, const QByteArray& message)
{
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
    default:
        Log_Error()<<"[handleUdpMessage]未知类型:"<<clientMessage.content_case();
        break;
    }
}

void NetworkDispatcher::handleUdpConnection(const QHostAddress &address, quint16 port, const ClientConnectMessage &message)
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

QSharedPointer<Client> NetworkDispatcher::findClient(qint64 clientId)
{
    QSharedPointer<Client> client=m_clientsMap.value(clientId);
    return client;
}

quint64 NetworkDispatcher::addClient()
{
    quint64 clientId=nextClientId;
    nextClientId++;
    m_clientsMap[clientId] = QSharedPointer<Client>::create(clientId);
    return clientId;
}

void NetworkDispatcher::bindClient(const quint64 clientId, QTcpSocket* tcpSocket)
{
    if(m_clientsMap.contains(clientId))
    {
        m_tcpClientsMap[tcpSocket]=clientId;
        Log_Info()<<"[bindClient]绑定客户端id:[Tcp:"<<m_tcpServer.getTcpSocketInfo(tcpSocket)<<"-"<<clientId<<"]";
    }
}

void NetworkDispatcher::bindClient(const quint64 clientId, const UdpEndPoint& udpEndPoint)
{
    if(m_clientsMap.contains(clientId))
    {
        m_udpClientsMap[udpEndPoint]=clientId;
        Log_Info()<<"[bindClient]绑定客户端id:[Udp:"<<m_udpServer.getPeerAddressInfo(udpEndPoint)<<"-"<<clientId<<"]";
    }
}

