#include <QApplication>
#include <QPushButton>
#include "TcpServer.h"

int main(int argc, char* argv[])
{
    QApplication a(argc, argv);
    qDebug()<<"Hello World";
    TcpServer server;


    return QApplication::exec();
}