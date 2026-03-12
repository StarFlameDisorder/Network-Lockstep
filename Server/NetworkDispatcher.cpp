//
// Created by StarFlame on 2026/2/28.
//

#include "NetworkDispatcher.h"

NetworkDispatcher::NetworkDispatcher():m_tcpServer(this),m_udpServer(this)
{
}

void NetworkDispatcher::sendTcpMessage(QTcpSocket *socket,const QByteArray &message)
{
    m_tcpServer.sendMessageBySocket(socket,message);
}

void NetworkDispatcher::handleTcpMessage(QTcpSocket* socket, const QByteArray& message)
{

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

