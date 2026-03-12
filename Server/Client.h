//
// Created by StarFlame on 2026/3/11.
//

#ifndef SERVER_CLIENT_H
#define SERVER_CLIENT_H
#include <QHostAddress>
#include <QTime>

class Client
{
public:
    Client(quint64 clientId);
    Client(quint64 clientId,const QHostAddress &address, const quint16 port);
    void updateActiveTime();
    QDateTime getActiveTime();
private:
    quint64 m_clientId;
    QHostAddress m_address;
    quint16 m_port;
    QDateTime m_activeTime;
};


#endif //SERVER_CLIENT_H