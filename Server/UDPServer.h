//
// Created by StarFlame on 2026/3/5.
//

#ifndef SERVER_UDPSERVER_H
#define SERVER_UDPSERVER_H

#include <QUdpSocket>

class UDPServer:public QObject
{
    Q_OBJECT
public:
    UDPServer(QObject *parent=nullptr);


private:
    void receiveSocketMessage();
    std::string getPeerAddressInfo(const QHostAddress& address,const quint16 &port)const;
    QUdpSocket *m_socket;
    quint64 m_udpNextId=0;
};


#endif //SERVER_UDPSERVER_H