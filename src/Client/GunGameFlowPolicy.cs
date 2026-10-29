using System;
namespace RaidRoulette.TestingClient;
public static class GunGameFlowPolicy
{

 public static bool RefillRocket(int applied,int desired,int pending,double emptySeconds)=>applied==7&&desired==7&&pending==0&&emptySeconds>=10;
}
