//
// Created by StarFlame on 2026/2/14.
//

#ifndef SERVER_TCPSERVER_H
#define SERVER_TCPSERVER_H
#include <QTcpServer>
#include <QHash>

class NetworkDispatcher;

class TcpServer:public QTcpServer
{
    Q_OBJECT
public:
    TcpServer(QObject *parent = nullptr);
    std::string getTcpSocketInfo(const QTcpSocket *socket)const;
    void sendMessage(QTcpSocket *socket,QByteArray message);
private:
    void tcpServerConnectionNew();
    void tcpServerConnectClosed();
    QByteArray receiveTcpMessage(QTcpSocket *socket);
    void receiveSocketMessage();

    QHash<QTcpSocket*,QByteArray> m_tcpMessageBuffer;

signals:
    void tcpReadyRead(QTcpSocket* socket,QByteArray data);
    void addNewClient(QTcpSocket* socket);
    void receiveMessage(QTcpSocket *socket,QByteArray message);
};


#endif //SERVER_TCPSERVER_H