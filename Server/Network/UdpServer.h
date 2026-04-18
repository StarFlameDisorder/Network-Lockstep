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
#include <QQueue>
#include <QTimer>

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

struct PendingPacket
{
    qint64 index;//包序号
    QByteArray sendData;
    qint64 previousTime; //上次发送时间(包括重传)
    int times; //发送尝试次数
    bool isAck; //是否确认
};

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
    void sendACKMessage(const QHostAddress& address,const quint16 &port,qint64 index);
    void checkAndResend();

    QUdpSocket *m_socket;
    //QSet<UdpEndPoint> m_udpEndPoints;
    QHash<UdpEndPoint,qint64> m_udpIndex;//帧序号
    QHash<UdpEndPoint,QHash<qint64,PendingPacket>> m_pendingPackets;//发送缓存
    //QHash<UdpEndPoint,QQueue<qint64>> m_sendQueue;//重传队列

    QHash<UdpEndPoint,QHash<qint64,QByteArray>> m_receiveBuf;//接收缓存
    QHash<UdpEndPoint,qint64> m_invokeIndex;//下一个应传出的序号
    QTimer m_timer;

signals:
    void receiveMessage(const QHostAddress& address, const quint16& port,const QByteArray& message);
};


#endif //SERVER_UDPSERVER_H