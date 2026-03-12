//
// Created by StarFlame on 2026/3/11.
//

#include "Client.h"


Client::Client(quint64 clientId)
    :m_clientId(clientId)
{
}

Client::Client(quint64 clientId,const QHostAddress& address, const quint16 port)
    :m_address(address), m_port(port),m_activeTime(QDateTime::currentDateTime()),m_clientId(clientId)
{
}

void Client::updateActiveTime()
{
    m_activeTime = QDateTime::currentDateTime();
}

QDateTime Client::getActiveTime()
{
    return m_activeTime;
}
