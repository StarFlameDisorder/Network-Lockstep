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


void NetworkDispatcher::sendUdpMessage(QHostAddress address, quint16 port, const QByteArray& message)
{
    m_udpServer.sendMessage(address,port,message);
}

void NetworkDispatcher::handleTcpMessage(QTcpSocket* socket, const QByteArray& message)
{
    ClientMessage clientMessage;
    clientMessage.ParseFromArray(message,message.size());
    switch (clientMessage.content_case())
    {
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
    if(m_clientsMap.find(clientId)!=m_clientsMap.end())m_tcpClientsMap[tcpSocket]=clientId;
}

void NetworkDispatcher::bindClient(const quint64 clientId, const UdpEndPoint& udpEndPoint)
{
    if(m_clientsMap.find(clientId)!=m_clientsMap.end())m_udpClientsMap[udpEndPoint]=clientId;
}

