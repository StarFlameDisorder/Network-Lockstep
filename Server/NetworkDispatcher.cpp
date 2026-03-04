//
// Created by StarFlame on 2026/2/28.
//

#include "NetworkDispatcher.h"

NetworkDispatcher::NetworkDispatcher()
{
}

void NetworkDispatcher::sendTcpMessage(QTcpSocket *socket,const QByteArray &message)
{
    m_tcpServer.sendMessageBySocket(socket,message);
}

void NetworkDispatcher::handleTcpMessage(QTcpSocket* socket, const QByteArray& message)
{

}
