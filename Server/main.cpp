#include <QApplication>
#include <QPushButton>

int main(int argc, char* argv[])
{
    QApplication a(argc, argv);
    qDebug()<<"Hello World";
    return QApplication::exec();
}