//
// Created by StarFlame on 2026/2/14.
//

#include "TcpServer.h"
#include <QDebug>

TcpServer::TcpServer(QObject* parent):QTcpServer(parent)
{
    qDebug()<<"初始化服务器";
    connect(this,&QTcpServer::newConnection,this,&TcpServer::tcpServerConnectionNew);
    listen(QHostAddress::Any, 1975);
}

void TcpServer::tcpServerConnectionNew()
{
    qDebug()<<"新连接";
}
