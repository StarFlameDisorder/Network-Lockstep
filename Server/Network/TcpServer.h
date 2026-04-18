/*
* Created by StarFlame on 2026/2/14.
 * TcpSocket通信
 * 支持分包黏包处理
 */

#ifndef SERVER_TCPSERVER_H
#define SERVER_TCPSERVER_H
#include <QTcpServer>
#include <QTcpSocket>
#include <QHash>

struct TcpEndPoint
{
    TcpEndPoint(){};

    TcpEndPoint(QTcpSocket *socket)
        :address(socket->peerAddress()),port(socket->peerPort())
    {}

    TcpEndPoint(const QHostAddress &adr,const quint16 &port)
        :address(adr),port(port)
    {}

    bool operator==(const TcpEndPoint& other)const
    {
        return address==other.address && port==other.port;
    }

    QHostAddress address;
    quint16 port;
};

inline size_t qHash(const TcpEndPoint& key,uint seed=0)
{
    return qHashMulti(seed,key.address,key.port);
}

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
    void deleteClient(QTcpSocket* socket);
};


#endif //SERVER_TCPSERVER_H