#include <QApplication>
#include <QPushButton>
#include "GameServer.h"


int main(int argc, char* argv[])
{
    QApplication a(argc, argv);
    qDebug()<<"Hello World";
    //TcpServer server;
    GameServer server;

    return QApplication::exec();
}