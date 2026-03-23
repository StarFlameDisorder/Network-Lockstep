//
// Created by StarFlame on 2026/3/5.
//

#ifndef SERVER_UDPSERVER_H
#define SERVER_UDPSERVER_H

#include <QSet>
#include <QUdpSocket>
#include <QHash>

struct UdpEndPoint
{
    UdpEndPoint(){};
    UdpEndPoint(const QHostAddress &adr,const quint16 &port)
        :address(adr),port(port)
    {}

    bool operator==(const UdpEndPoint& other)const
    {
        return address==other.address && port==other.port;
    }

    QHostAddress address;
    quint16 port;
};

inline size_t qHash(const UdpEndPoint& key,uint seed=0)
{
    return qHashMulti(seed,key.address,key.port);
}

class NetworkDispatcher;

class UdpServer:public QObject
{
    Q_OBJECT
public:
    UdpServer(NetworkDispatcher *networkDispatcher,QObject *parent=nullptr);
    void sendMessage(const QHostAddress& address,const quint16& port,const QByteArray& message);
    std::string getPeerAddressInfo(const QHostAddress& address,const quint16 &port)const;
    std::string getPeerAddressInfo(const UdpEndPoint& udpEndPoint)const;
private:
    void receiveSocketMessage();
    void receiveMessage(const QHostAddress& address,const quint16& port,const QByteArray& message);


    QUdpSocket *m_socket;
    QSet<UdpEndPoint> m_udpEndPoints;
    NetworkDispatcher *_networkDispatcher;
signals:
    void udpReadyRead(const QHostAddress& address,const quint16& port,const QByteArray& message);

};


#endif //SERVER_UDPSERVER_H