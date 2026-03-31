/*
 * Created by StarFlame on 2026/3/5.
 * UDPSocket通信
 * 可靠UDP待实现
 */

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

class UdpServer:public QObject
{
    Q_OBJECT
public:
    UdpServer(QObject *parent=nullptr);
    void sendMessage(const QHostAddress& address,const quint16& port,const QByteArray& message);
    std::string getPeerAddressInfo(const QHostAddress& address,const quint16 &port)const;
    std::string getPeerAddressInfo(const UdpEndPoint& udpEndPoint)const;
private:
    void receiveSocketMessage();

    QUdpSocket *m_socket;
    QSet<UdpEndPoint> m_udpEndPoints;
signals:
    void receiveMessage(const QHostAddress& address, const quint16& port,const QByteArray& message);
};


#endif //SERVER_UDPSERVER_H