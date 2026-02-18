//
// Created by StarFlame on 2026/2/14.
//

#ifndef SERVER_TCPSERVER_H
#define SERVER_TCPSERVER_H
#include <QTcpServer>


class TcpServer:public QTcpServer
{
public:
    TcpServer(QObject *parent = nullptr);
private slots:
    void tcpServerConnectionNew();
    void tcpServerConnectionClosed();

};


#endif //SERVER_TCPSERVER_H