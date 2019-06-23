#include <WinSock2.h>
#include <ws2tcpip.h>
#include <stdio.h>

#pragma warning(disable : 4996)

// Need to link with Ws2_32.lib
#pragma comment(lib, "ws2_32.lib")

typedef unsigned char U8;
typedef unsigned short U16;

struct ipheader
{
	U8 length : 4;// : 4;
	U8 version : 4;

	U8 differentiated_services_field;
	U16 total_length;
	U16 ident;
	U16 fragment; // fragment offset // first 8 bit = flags not 8 bit
	U8 ttl; // time to live
	U8 protocol;
	U16 checksum;
	in_addr src;
	in_addr dst;
};

struct udpheader
{
	U16 srcport;
	U16 dstport;
	U16 length;
	U16 checksum;
};


#define CON_SERVER_IP           "1.0.0.127"
//#define CON_SERVER_IP           "127.0.0.1"
#define CON_SERVER_PORT         34568

#define MY_PORT                 34567
#define MY_IP                   "1.0.0.127"

#define DATA_LENGTH 16
#define WHOLE_LENGTH (20+8+DATA_LENGTH)

// http://www.c-worker.ch/tuts/raw_icmp.php
unsigned short Checksum(unsigned short* p_usBuffer, int iSize)
{
	unsigned long lCheckSum = 0;
	while (iSize > 1)
	{
		lCheckSum += *p_usBuffer++;
		iSize -= sizeof(unsigned short);
	}
	if (iSize)
		lCheckSum += *(unsigned char*)p_usBuffer; // + 0 entfällt, da überflüßig
	lCheckSum = ((lCheckSum >> 16) + (lCheckSum & 0xffff)) + (lCheckSum >> 16);
	return (unsigned short)(~lCheckSum);
}
int main(int argc, char* argv[])
{
	WSADATA wsa;
	WSAStartup(MAKEWORD(2, 0), &wsa);

	char packet[WHOLE_LENGTH];

	ZeroMemory(packet, WHOLE_LENGTH);

	ipheader* pIpHeader = (ipheader*)& packet;
	udpheader* pUDPHeader = (udpheader*)& packet + sizeof(ipheader);

	//SOCKADDR_IN sAddr;
	//ZeroMemory(&sAddr, sizeof(SOCKADDR_IN));
	//sAddr.sin_family = AF_INET;
	//sAddr.sin_port = htons(CON_SERVER_PORT);
	//inet_pton(AF_INET, CON_SERVER_IP, &sAddr.sin_addr);

	SOCKADDR_IN sAddr;
	ZeroMemory(&sAddr, sizeof(SOCKADDR_IN));
	sAddr.sin_family = AF_INET;
	sAddr.sin_port = htons(CON_SERVER_PORT);
	sAddr.sin_addr.s_addr = inet_addr(CON_SERVER_IP); // thank you BevynQ

	pIpHeader->length = 5;
	pIpHeader->version = 4;
	pIpHeader->differentiated_services_field = 0;
	pIpHeader->total_length = sizeof(ipheader) + sizeof(udpheader) + DATA_LENGTH;
	pIpHeader->ident = htonl(54321);
	pIpHeader->fragment = 0;
	pIpHeader->ttl = 65;
	pIpHeader->protocol = IPPROTO_UDP;
	pIpHeader->checksum = 0;
	pIpHeader->src.s_addr = inet_addr(MY_IP);
	pIpHeader->dst.s_addr = inet_addr(CON_SERVER_IP);

	pUDPHeader->srcport = htons(MY_PORT);
	pUDPHeader->dstport = htons(CON_SERVER_PORT);
	pUDPHeader->length = sizeof(udpheader) + DATA_LENGTH;
	pUDPHeader->checksum = 0;

	strcpy(packet + sizeof(ipheader) + sizeof(udpheader), "Lets go\n");

	pUDPHeader->checksum = Checksum((unsigned short*)packet + sizeof(ipheader), sizeof(udpheader) + DATA_LENGTH);

	pIpHeader->checksum = Checksum((unsigned short*)packet, WHOLE_LENGTH);

	printf("UDPHeader checksum:\t0x%04X\n", pUDPHeader->checksum);
	printf("IPHeader checksum:\t0x%04X\n", pIpHeader->checksum);


	SOCKET sock = socket(AF_INET, SOCK_RAW, IPPROTO_RAW);
	if (sock == INVALID_SOCKET)
	{
		printf("socket generation failed (%d)\n", WSAGetLastError());
		printf("\n\n\n\nDone\n");
		getchar();
		return 0;
	}

	int i = sendto(sock, packet, WHOLE_LENGTH, 0, (SOCKADDR*)& sAddr, sizeof(SOCKADDR_IN));
	printf("sendto: %i (%d)\n", i, WSAGetLastError());

	printf("\n\n\n\nDone\n");
	getchar();
	return 0;
}