#include <QApplication>
#include <QPushButton>
#include "GameServer.h"
#include "protobuf/output/test.pb.h"


int main(int argc, char* argv[])
{
    QApplication a(argc, argv);
    qDebug()<<"Hello World";

    GameServer server;

    return QApplication::exec();
}