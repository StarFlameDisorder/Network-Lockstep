//
// Created by StarFlame on 2026/2/14.
//

#ifndef SERVER_TCPSERVER_H
#define SERVER_TCPSERVER_H
#include <QTcpServer>
#include <QHash>

class TcpServer:public QTcpServer
{
public:
    TcpServer(QObject *parent = nullptr);
private:
    void tcpServerConnectionNew();
    void tcpServerConnectClosed();
    std::string getTcpSocketInfo(const QTcpSocket *socket)const;

    QHash<quint64,QTcpSocket*> m_idTcpSocketMap;
};


#endif //SERVER_TCPSERVER_H