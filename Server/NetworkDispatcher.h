/*
 * Created by StarFlame on 2026/2/28.
 * 消息分发处理
 */

#ifndef SERVER_NETWORKDISPATCHER_H
#define SERVER_NETWORKDISPATCHER_H

#include "TcpServer.h"
#include "UDPServer.h"

class NetworkDispatcher
{
public:
    NetworkDispatcher();
    void sendTcpMessage(QTcpSocket *socket,const QByteArray &message);
    void handleTcpMessage(QTcpSocket *socket,const QByteArray &message);

private:
    TcpServer m_tcpServer;
    UdpServer m_udpServer;
};


#endif //SERVER_NETWORKDISPATCHER_H