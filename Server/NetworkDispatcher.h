/*
 * Created by StarFlame on 2026/2/28.
 * 消息分发处理
 */

#ifndef SERVER_NETWORKDISPATCHER_H
#define SERVER_NETWORKDISPATCHER_H

#include "TcpServer.h"

class NetworkDispatcher
{
public:
    NetworkDispatcher();
    void sendTcpMessage(QTcpSocket *socket,const QByteArray &message);
    void handleTcpMessage(QTcpSocket *socket,const QByteArray &message);

private:
    TcpServer m_tcpServer;
};


#endif //SERVER_NETWORKDISPATCHER_H